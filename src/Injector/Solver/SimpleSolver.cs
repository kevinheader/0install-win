﻿/*
 * Copyright 2010-2012 Bastian Eicher
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser Public License for more details.
 * 
 * You should have received a copy of the GNU Lesser Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using Common.Collections;
using ZeroInstall.Injector.Feeds;
using ZeroInstall.Injector.Properties;
using ZeroInstall.Model;

namespace ZeroInstall.Injector.Solver
{
    /// <summary>
    /// Solves simple tree-like dependencies (no support for loops, diamonds, etc.).
    /// </summary>
    /// <remarks>This class is immutable and thread-safe.</remarks>
    public sealed class SimpleSolver : ISolver
    {
        /// <inheritdoc/>
        public Selections Solve(Requirements requirements, Policy policy, out bool staleFeeds)
        {
            #region Sanity checks
            if (requirements == null) throw new ArgumentNullException("requirements");
            if (policy == null) throw new ArgumentNullException("policy");
            #endregion

            return new SolveProcess(policy).Run(requirements, out staleFeeds);
        }

        private class SolveProcess
        {
            private readonly Policy _policy;
            private readonly SolverFeedCache _feedCache;
            private Selections _selections;

            public SolveProcess(Policy policy)
            {
                _policy = policy;
                _feedCache = new SolverFeedCache(policy);
            }

            public Selections Run(Requirements requirements, out bool staleFeeds)
            {
                var mainRequirements = requirements.Clone();

                // Default to 'run' or 'compile' command if not specified
                if (requirements.CommandName == null)
                    requirements.CommandName = (requirements.Architecture.Cpu == Cpu.Source ? Command.NameCompile : Command.NameRun);

                _selections = new Selections {InterfaceID = requirements.InterfaceID, CommandName = requirements.CommandName};
                AddSelection(mainRequirements);

                staleFeeds = _feedCache.StaleFeeds;
                return _selections;
            }

            private void AddSelection(Requirements requirements)
            {
                //policy.Handler.CancellationToken.ThrowIfCancellationRequested();

                var candidates = new List<SelectionCandidate>();

                var interfacePreferences = InterfacePreferences.LoadForSafe(requirements.InterfaceID);

                var feeds = new Dictionary<string, FeedPreferences>();
                var feedPreferences = FeedPreferences.LoadForSafe(requirements.InterfaceID);
                feeds.Add(requirements.InterfaceID, feedPreferences);

                AddCandidatesFromFeed(candidates, requirements.InterfaceID, feedPreferences, requirements);
                //LoadAdditionalFeeds(interfacePreferences.Feeds, ...);

                SortCandidates(candidates, interfacePreferences);

                var bestCandidate = candidates.Find(candidate => candidate.IsUsable);
                if (bestCandidate == null) throw new SolverException("No fitting candidate!");
                var implementation = bestCandidate.Implementation;

                var selection = new ImplementationSelection(feeds, candidates)
                {
                    InterfaceID = requirements.InterfaceID,
                    ID = implementation.ID,
                    ManifestDigest = implementation.ManifestDigest,
                    Architecture = implementation.Architecture,
                    Version = implementation.Version,
                    Released = implementation.Released,
                    License = implementation.License,
                };
                selection.Dependencies.AddAll(implementation.Dependencies);
                selection.Bindings.AddAll(implementation.Bindings);
                if (bestCandidate.FeedID != requirements.InterfaceID) selection.FromFeed = bestCandidate.FeedID;
                if (!string.IsNullOrEmpty(requirements.CommandName))
                {
                    var command = implementation.GetCommand(requirements.CommandName);
                    if (command.Runner != null) throw new SolverException("Unable to handle <runner>s!");
                    selection.Commands.Add(command);
                }
                _selections.Implementations.Add(selection);

                // ToDo: Detect cyclic or cross-references
                foreach (var dependency in implementation.Dependencies)
                {
                    AddSelection(new Requirements
                    {
                        InterfaceID = dependency.Interface,
                        Architecture = requirements.Architecture,
                        //BeforeVersion = dependency.
                    });
                }
            }

            private void AddCandidatesFromFeed(ICollection<SelectionCandidate> candidates, string feedID, FeedPreferences feedPreferences, Requirements requirements)
            {
                var feed = _feedCache.GetFeed(feedID);

                // ToDo: Add support for PackageImplementations
                foreach (var implementation in EnumerableUtils.OfType<Implementation>(feed.Elements))
                {
                    // ToDo: Check it is a valid implementation (has version number, manifest digest, etc.)

                    // Only list implementations that provide the requested command
                    if (!string.IsNullOrEmpty(requirements.CommandName))
                        if (!implementation.Commands.Exists(command => command.Name == requirements.CommandName)) continue;

                    var candidate = new SelectionCandidate(feedID, implementation, feedPreferences.GetImplementationPreferences(implementation.ID), requirements);

                    // Exclude non-cached implementations when in offline-mode
                    if (candidate.IsUsable && _policy.Config.NetworkUse == NetworkLevel.Offline && !_policy.Fetcher.Store.Contains(implementation.ManifestDigest))
                    {
                        candidate.IsUsable = false;
                        candidate.Notes = Resources.SelectionCandidateNoteNotCached;
                    }

                    candidates.Add(candidate);
                }

                //LoadAdditionalFeeds(feed.Feeds, ...);
            }

            private void SortCandidates(List<SelectionCandidate> candidates, InterfacePreferences interfacePreferences)
            {
                var stabilityPolicy = interfacePreferences.StabilityPolicy;
                if (stabilityPolicy == Stability.Unset) stabilityPolicy = _policy.Config.HelpWithTesting ? Stability.Testing : Stability.Stable;

                candidates.Sort((x, y) =>
                {
                    // Preferred implementations come first
                    if (x.EffectiveStability == Stability.Preferred && y.EffectiveStability != Stability.Preferred) return -1;
                    if (x.EffectiveStability != Stability.Preferred && y.EffectiveStability == Stability.Preferred) return 1;

                    // If network use is set to 'Minimal', cached implementations come before non-cached
                    if (_policy.Config.NetworkUse == NetworkLevel.Minimal)
                    {
                        bool xCached = _policy.Fetcher.Store.Contains(x.Implementation.ManifestDigest);
                        bool yCached = _policy.Fetcher.Store.Contains(x.Implementation.ManifestDigest);

                        if (xCached && !yCached) return -1;
                        if (!xCached && yCached) return 1;
                    }

                    // Implementations at or above the selected stability level come before all others (smaller enum value = more stable)
                    if (x.EffectiveStability <= stabilityPolicy && y.EffectiveStability > stabilityPolicy) return -1;
                    if (x.EffectiveStability > stabilityPolicy && y.EffectiveStability <= stabilityPolicy) return 1;

                    // Higher-numbered versions come before low-numbered ones
                    if (x.Version > y.Version) return -1;
                    if (x.Version < y.Version) return 1;

                    // Cached come before non-cached (for 'Full' network use mode)
                    if (_policy.Config.NetworkUse == NetworkLevel.Full)
                    {
                        bool xCached = _policy.Fetcher.Store.Contains(x.Implementation.ManifestDigest);
                        bool yCached = _policy.Fetcher.Store.Contains(x.Implementation.ManifestDigest);

                        if (xCached && !yCached) return -1;
                        if (!xCached && yCached) return 1;
                    }
                    return 0;
                });
            }
        }
    }
}
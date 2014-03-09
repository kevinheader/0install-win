﻿/*
 * Copyright 2010 Roland Leopold Walkling, Bastian Eicher
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
using System.IO;
using System.Linq;
using System.Threading;
using Common.Storage;
using Common.Tasks;
using Common.Utils;
using NUnit.Framework;
using ZeroInstall.Store.Model;

namespace ZeroInstall.Store.Implementations
{
    /// <summary>
    /// Contains test methods for <see cref="DirectoryStore"/>.
    /// </summary>
    [TestFixture]
    public class DirectoryStoreTest
    {
        private TemporaryDirectory _tempDir;
        private DirectoryStore _store;

        [SetUp]
        public void SetUp()
        {
            _tempDir = new TemporaryDirectory("0install-unit-tests");
            _store = new DirectoryStore(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            _tempDir.Dispose();
        }

        [Test]
        public void TestContains()
        {
            Directory.CreateDirectory(Path.Combine(_tempDir, "sha256new_123ABC"));
            Assert.IsTrue(_store.Contains(new ManifestDigest(sha256New: "123ABC")));
            Assert.IsFalse(_store.Contains(new ManifestDigest(sha256New: "456XYZ")));
        }

        [Test]
        public void TestListAll()
        {
            Directory.CreateDirectory(Path.Combine(_tempDir, "sha1=test1"));
            Directory.CreateDirectory(Path.Combine(_tempDir, "sha1new=test2"));
            Directory.CreateDirectory(Path.Combine(_tempDir, "sha256=test3"));
            Directory.CreateDirectory(Path.Combine(_tempDir, "sha256new_test4"));
            Directory.CreateDirectory(Path.Combine(_tempDir, "temp=stuff"));
            CollectionAssert.AreEqual(new[]
            {
                new ManifestDigest(sha1: "test1"),
                new ManifestDigest(sha1New: "test2"),
                new ManifestDigest(sha256: "test3"),
                new ManifestDigest(sha256New: "test4")
            }, _store.ListAll());
        }

        [Test]
        public void TestListAllTemp()
        {
            Directory.CreateDirectory(Path.Combine(_tempDir, "sha1=test"));
            Directory.CreateDirectory(Path.Combine(_tempDir, "temp=stuff"));
            CollectionAssert.AreEqual(new[] {Path.Combine(_tempDir, "temp=stuff")}, _store.ListAllTemp());
        }

        [Test]
        public void TestOptimise()
        {
            var handler = new SilentTaskHandler();

            string package1Path = Path.Combine(_tempDir, "sha256=1");
            new PackageBuilder()
                .AddFile("fileA", "abc", new DateTime(2000, 1, 1))
                .AddFolder("dir").AddFile("fileB", "abc", new DateTime(2000, 1, 1))
                .WritePackageInto(package1Path);
            Manifest.CreateDotFile(package1Path, ManifestFormat.Sha256, handler);

            string package2Path = Path.Combine(_tempDir, "sha256=2");
            new PackageBuilder()
                .AddFile("fileA", "abc", new DateTime(2000, 2, 2))
                .AddFolder("dir").AddFile("fileB", "def", new DateTime(2000, 2, 2))
                .WritePackageInto(package2Path);
            Manifest.CreateDotFile(package2Path, ManifestFormat.Sha256, handler);

            string package3Path = Path.Combine(_tempDir, "sha256new_3");
            new PackageBuilder()
                .AddFile("fileA", "abc", new DateTime(2000, 1, 1))
                .WritePackageInto(package3Path);
            Manifest.CreateDotFile(package3Path, ManifestFormat.Sha256New, handler);

            _store.Optimise(new SilentTaskHandler());

            Assert.IsTrue(FileUtils.AreHardlinked(
                Path.Combine(package1Path, "fileA"),
                Path.Combine(package1Path, "dir", "fileB")),
                message: "Identical files within implementations should be hardlinked.");
            Assert.IsTrue(FileUtils.AreHardlinked(
                Path.Combine(package1Path, "fileA"),
                Path.Combine(package2Path, "fileA")),
                message: "Identical files across implementations with the same manifest format should be hardlinked.");
            Assert.IsFalse(FileUtils.AreHardlinked(
                Path.Combine(package1Path, "dir", "fileB"),
                Path.Combine(package2Path, "dir", "fileB")),
                message: "Different files should not be hardlinked.");
            Assert.IsFalse(FileUtils.AreHardlinked(
                Path.Combine(package1Path, "fileA"),
                Path.Combine(package3Path, "fileA")),
                message: "Files across manifest format borders should not be hardlinked.");
        }

        [Test]
        public void ShouldAllowToAddFolder()
        {
            using (var packageDir = new TemporaryDirectory("0install-unit-tests"))
            {
                var digest = new ManifestDigest(Manifest.CreateDotFile(packageDir, ManifestFormat.Sha256, new MockHandler()));
                _store.AddDirectory(packageDir, digest, new MockHandler());

                Assert.IsTrue(_store.Contains(digest), "After adding, Store must contain the added package");
                CollectionAssert.AreEqual(new[] {digest}, _store.ListAll(), "After adding, Store must show the added package in the complete list");
            }
        }

        [Test]
        public void ShouldRecreateMissingStoreDir()
        {
            Directory.Delete(_tempDir, recursive: true);

            using (var packageDir = new TemporaryDirectory("0install-unit-tests"))
            {
                var digest = new ManifestDigest(Manifest.CreateDotFile(packageDir, ManifestFormat.Sha256, new MockHandler()));
                _store.AddDirectory(packageDir, digest, new MockHandler());

                Assert.IsTrue(_store.Contains(digest), "After adding, Store must contain the added package");
                CollectionAssert.AreEqual(new[] {digest}, _store.ListAll(), "After adding, Store must show the added package in the complete list");

                Assert.IsTrue(Directory.Exists(_tempDir), "Store directory should have been recreated");
            }
        }

        [Test]
        public void ShouldHandleRelativePaths()
        {
            // Change the working directory
            string oldWorkingDir = Environment.CurrentDirectory;
            Environment.CurrentDirectory = _tempDir;

            try
            {
                _store = new DirectoryStore(".");
                ShouldAllowToAddFolder();
            }
            finally
            {
                // Restore the original working directory
                Environment.CurrentDirectory = oldWorkingDir;
            }
        }

        [Test]
        public void ShouldAllowToRemove()
        {
            string implPath = Path.Combine(_tempDir, "sha256new_123ABC");
            Directory.CreateDirectory(implPath);

            _store.Remove(new ManifestDigest(sha256New: "123ABC"));
            Assert.IsFalse(Directory.Exists(implPath), "After remove, Store may no longer contain the added package");
        }

        [Test]
        public void ShouldReturnCorrectPathOfPackageInCache()
        {
            string implPath = Path.Combine(_tempDir, "sha256new_123ABC");
            Directory.CreateDirectory(implPath);
            Assert.AreEqual(implPath, _store.GetPath(new ManifestDigest(sha256New: "123ABC")), "Store must return the correct path for Implementations it contains");
        }

        [Test]
        public void ShouldThrowWhenRequestedPathOfUncontainedPackage()
        {
            Assert.IsNull(_store.GetPath(new ManifestDigest(sha256: "123")));
        }

        [Test]
        public void ShouldDetectDamagedImplementations()
        {
            using (var packageDir = new TemporaryDirectory("0install-unit-tests"))
            {
                new PackageBuilder().AddFolder("subdir")
                    .AddFile("file", "AAA", new DateTime(2000, 1, 1))
                    .WritePackageInto(packageDir);

                var digest = new ManifestDigest(Manifest.CreateDotFile(packageDir, ManifestFormat.Sha1New, new MockHandler()));
                _store.AddDirectory(packageDir, digest, new MockHandler());

                // After correctly adding a directory, the store should be valid
                Assert.IsEmpty(_store.Audit(new MockHandler()));

                // A contaminated store should be detected
                Directory.CreateDirectory(Path.Combine(_tempDir, "sha1new=abc"));
                DigestMismatchException problem = _store.Audit(new MockHandler()).First();
                Assert.AreEqual("sha1new=abc", problem.ExpectedHash);
                Assert.AreEqual("sha1new=da39a3ee5e6b4b0d3255bfef95601890afd80709", problem.ActualHash);
            }
        }

        [Test]
        public void StressTest()
        {
            using (var packageDir = new TemporaryDirectory("0install-unit-tests"))
            {
                new PackageBuilder().AddFolder("subdir")
                    .AddFile("file", "AAA", new DateTime(2000, 1, 1))
                    .WritePackageInto(packageDir);

                var digest = new ManifestDigest(Manifest.CreateDotFile(packageDir, ManifestFormat.Sha256, new MockHandler()));

                Exception exception = null;
                var threads = new Thread[100];
                for (int i = 0; i < threads.Length; i++)
                {
                    threads[i] = new Thread(() =>
                    {
                        try
                        {
                            // ReSharper disable once AccessToDisposedClosure
                            _store.AddDirectory(packageDir, digest, new MockHandler());
                            _store.Remove(digest);
                        }
                        catch (ImplementationAlreadyInStoreException)
                        {}
                        catch (ImplementationNotFoundException)
                        {}
                        catch (Exception ex)
                        {
                            exception = ex;
                        }
                    });
                    threads[i].Start();
                }

                foreach (var thread in threads)
                    thread.Join();
                if (exception != null)
                    Assert.Fail(exception.ToString());

                Assert.IsFalse(_store.Contains(digest));
            }
        }
    }
}

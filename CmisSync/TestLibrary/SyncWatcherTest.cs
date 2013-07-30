using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;

using CmisSync.Lib;


namespace TestLibrary
{
    using NUnit.Framework;

    [TestFixture]
    public class SyncWatcherTest
    {
        private static readonly string TestFolderParent = Directory.GetCurrentDirectory();
        private static readonly string TestFolder = Path.Combine(TestFolderParent, "test");
        private static readonly int NormalNumber = 10;
        private static readonly int HeavyNumber = 10000;
        private static readonly int FileInFolderNumber = 1000;

        private static int TestNumber;

        [TestFixtureSetUp]
        public void ClassInit()
        {
            Directory.CreateDirectory(TestFolder);
        }

        [TestFixtureTearDown]
        public void ClassCleanup()
        {
            if (Directory.Exists(TestFolder))
            {
                Directory.Delete(TestFolder, true);
            }
        }

        [SetUp]
        public void TestInit()
        {
        }

        [Test, Category("Fast")]
        public void TestEnableRaisingEvents()
        {
            using (Watcher watcher = new Watcher(TestFolder))
            {
                CreateTestFile(1);
                WaitWatcher();
                Assert.AreEqual(watcher.GetChangeList().Count, 0);

                watcher.EnableRaisingEvents = true;

                CreateTestFile(2);
                string name = GetPathname();
                WaitWatcher();
                Assert.AreEqual(1, watcher.GetChangeList().Count);
                Assert.AreEqual(name, watcher.GetChangeList()[0]);

                CreateTestFile(3);
                name = GetPathname();
                WaitWatcher();
                Assert.AreEqual(2, watcher.GetChangeList().Count);
                Assert.AreEqual(name, watcher.GetChangeList()[1]);

                watcher.EnableRaisingEvents = false;

                CreateTestFile(4);
                WaitWatcher();
                Assert.AreEqual(2, watcher.GetChangeList().Count);

                watcher.EnableRaisingEvents = true;

                CreateTestFile(5);
                name = GetPathname();
                WaitWatcher();
                Assert.AreEqual(3, watcher.GetChangeList().Count);
                Assert.AreEqual(name, watcher.GetChangeList()[2]);
            }
        }

        [Test, Category("Fast")]
        public void TestRemove()
        {
            Assert.Fail("TODO");
        }

        [Test, Category("Fast")]
        public void TestIgnore()
        {
            Assert.Fail("TODO");
        }

        [Test, Category("Fast")]
        public void TestChangeTypeCreated()
        {
            using (Watcher watcher = new Watcher(TestFolder))
            {
                watcher.EnableRaisingEvents = true;

                List<string> names = new List<string>();
                for (int i = 0; i < NormalNumber; ++i)
                {
                    CreateTestFile();
                    names.Add(GetPathname());
                }
                WaitWatcher();
                Assert.AreEqual(NormalNumber, watcher.GetChangeList().Count);
                for (int i = 0; i < NormalNumber; ++i)
                {
                    Assert.AreEqual(names[i], watcher.GetChangeList()[i]);
                    Assert.AreEqual(
                        Watcher.ChangeTypes.Created,
                        watcher.GetChangeType((string)watcher.GetChangeList()[i]));
                }
            }
        }

        [Test, Category("Slow")]
        public void TestChangeTypeCreatedHeavy()
        {
            using (Watcher watcher = new Watcher(TestFolder))
            {
                watcher.EnableRaisingEvents = true;

                for (int i = 0; i < HeavyNumber; ++i)
                {
                    CreateTestFile(0, i / FileInFolderNumber);
                }
                WaitWatcher();
                int totalNumber = HeavyNumber + (HeavyNumber - 1) / FileInFolderNumber;
                Assert.AreEqual(watcher.GetChangeList().Count, totalNumber);
                int fileNumber = 0;
                for (int i = 0; i < watcher.GetChangeList().Count; ++i)
                {
                    if (File.Exists(watcher.GetChangeList()[i]))
                    {
                        Assert.AreEqual(
                            watcher.GetChangeType((string)watcher.GetChangeList()[i]),
                            Watcher.ChangeTypes.Created);
                        ++fileNumber;
                    }
                }
                Assert.AreEqual(fileNumber, HeavyNumber);
            }
        }

        [Test, Category("Fast")]
        public void TestChangeTypeChanged()
        {
            using (Watcher watcher = new Watcher(TestFolder))
            {
                watcher.EnableRaisingEvents = true;

                List<string> names = new List<string>();
                for (int i = 0; i < NormalNumber; ++i)
                {
                    CreateTestFile();
                    names.Add(GetPathname());
                }
                for (int i = 0; i < NormalNumber; ++i)
                {
                    CreateTestFile(names[i], i + 1);
                    names.Add(GetPathname());
                }
                WaitWatcher();
                Assert.AreEqual(watcher.GetChangeList().Count, NormalNumber);
                for (int i = 0; i < NormalNumber; ++i)
                {
                    Assert.AreEqual(names[i], watcher.GetChangeList()[i]);
                    Assert.AreEqual(
                        watcher.GetChangeType((string)watcher.GetChangeList()[i]),
                        Watcher.ChangeTypes.Changed);
                }
            }
        }

        [Test, Category("Slow")]
        public void TestChangeTypeChangedHeavy()
        {
            Assert.Fail("TODO");
        }

        [Test, Category("Fast")]
        public void TestChangeTypeDeleted()
        {
            using (Watcher watcher = new Watcher(TestFolder))
            {
                watcher.EnableRaisingEvents = true;

                List<string> names = new List<string>();
                for (int i = 0; i < NormalNumber; ++i)
                {
                    CreateTestFile();
                    names.Add(GetPathname());
                    File.Delete(names[i]);
                }
                WaitWatcher();
                Assert.AreEqual(NormalNumber, watcher.GetChangeList().Count);
                for (int i = 0; i < NormalNumber; ++i)
                {
                    Assert.AreEqual(names[i], watcher.GetChangeList()[i]);
                    Assert.AreEqual(
                        watcher.GetChangeType((string)watcher.GetChangeList()[i]),
                        Watcher.ChangeTypes.Deleted);
                }
            }
        }

        [Test, Category("Slow")]
        public void TestChangeTypeDeleteHeavy()
        {
            Assert.Fail("TODO");
        }

        [Test, Category("Fast")]
        public void TestChangeTypeNone()
        {
            using (Watcher watcher = new Watcher(TestFolder))
            {
                watcher.EnableRaisingEvents = true;
                Assert.AreEqual(Watcher.ChangeTypes.None, watcher.GetChangeType(GetPathname()));
            }
        }

        [Test, Category("Fast")]
        public void TestChangeTypeForMove()
        {
            string oldnameOut = Path.Combine(TestFolderParent, "test.old");
            string newnameOut = Path.Combine(TestFolderParent, "test.new");
            string oldname = Path.Combine(TestFolder, "test.old");
            string newname = Path.Combine(TestFolder, "test.new");

            using (Watcher watcher = new Watcher(TestFolder))
            {
                watcher.EnableRaisingEvents = true;
                CreateTestFile(oldname, 1);
                File.Move(oldname, newname);
                WaitWatcher();
                Assert.AreEqual(watcher.GetChangeList().Count, 2);
                Assert.AreEqual(watcher.GetChangeList()[0], oldname);
                Assert.AreEqual(watcher.GetChangeList()[1], newname);
                Assert.AreEqual(watcher.GetChangeType(oldname), Watcher.ChangeTypes.Deleted);
                Assert.AreEqual(watcher.GetChangeType(newname), Watcher.ChangeTypes.Created);
                File.Delete(newname);
            }

            using (Watcher watcher = new Watcher(TestFolder))
            {
                watcher.EnableRaisingEvents = true;
                CreateTestFile(oldnameOut, 1);
                File.Move(oldnameOut, newname);
                WaitWatcher();
                Assert.AreEqual(watcher.GetChangeList().Count, 1);
                Assert.AreEqual(watcher.GetChangeList()[0], newname);
                Assert.AreEqual(watcher.GetChangeType(newname), Watcher.ChangeTypes.Created);
                File.Delete(newname);
            }

            using (Watcher watcher = new Watcher(TestFolder))
            {
                watcher.EnableRaisingEvents = true;
                CreateTestFile(oldname, 1);
                File.Move(oldname, newnameOut);
                WaitWatcher();
                Assert.AreEqual(watcher.GetChangeList().Count, 1);
                Assert.AreEqual(watcher.GetChangeList()[0], oldname);
                Assert.AreEqual(watcher.GetChangeType(oldname), Watcher.ChangeTypes.Deleted);
                File.Delete(newnameOut);
            }

            using (Watcher watcher = new Watcher(TestFolder))
            {
                watcher.EnableRaisingEvents = true;
                CreateTestFile(oldnameOut, 1);
                File.Move(oldnameOut, newnameOut);
                WaitWatcher();
                Assert.AreEqual(watcher.GetChangeList().Count, 0);
                File.Delete(newnameOut);
            }
        }

        [Test, Category("Slow")]
        public void TestChangeTypeForMoveHeavy()
        {
            Assert.Fail("TODO");
        }

        [Test, Category("Fast")]
        public void TestChangeTypeMix()
        {
            Assert.Fail("TODO");
        }

        [Test, Category("Slow")]
        public void TestChangeTypeMixHeavy()
        {
            Assert.Fail("TODO");
        }

        private string GetNextPathname(int level)
        {
            return GetPathname(Interlocked.Increment(ref TestNumber), level);
        }

        private string GetPathname(int level = 0)
        {
            return GetPathname(TestNumber, level);
        }

        private string GetPathname(int number,int level)
        {
            string pathname = TestFolder;
            for (int i = 1; i <= level; ++i)
            {
                pathname = System.IO.Path.Combine(pathname, String.Format("folder-{0}", i));
                if (!Directory.Exists(pathname))
                {
                    Directory.CreateDirectory(pathname);
                }
            }
            return System.IO.Path.Combine(pathname, String.Format("test-{0}.bin", number));
        }

        private void CreateTestFile(string name, int sizeInKB)
        {
            Random random = new Random();
            byte[] data = new byte[1024];

            using (FileStream stream = File.OpenWrite(name))
            {
                // Write random data
                for (int i = 0; i < sizeInKB; i++)
                {
                    random.NextBytes(data);
                    stream.Write(data, 0, data.Length);
                }
            }
        }

        private void CreateTestFile(int sizeInKB = 0, int level = 0)
        {
            Random random = new Random();
            byte[] data = new byte[1024];

            using (FileStream stream = File.OpenWrite(GetNextPathname(level)))
            {
                // Write random data
                for (int i = 0; i < sizeInKB; i++)
                {
                    random.NextBytes(data);
                    stream.Write(data, 0, data.Length);
                }
            }
        }

        private void WaitWatcher()
        {
            Thread.Sleep(10);
        }
    }
}

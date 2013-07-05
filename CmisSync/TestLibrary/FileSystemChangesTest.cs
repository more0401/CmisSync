using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;

using CmisSync.Lib.Sync;



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
        public void TestGet0()
        {
            Assert.AreEqual(0, FileSystemChanges.Get0());
        }

        [Test, Category("Fast")]
        public void TestEnableMonitorChanges()
        {
            FileSystemChanges changes = new FileSystemChanges(TestFolder);

            CreateTestFile(1);
            WaitWatcher();
            Assert.AreEqual(changes.ChangeList.Count, 0);

            changes.EnableMonitorChanges = true;

            CreateTestFile(2);
            string name = GetPathname();
            WaitWatcher();
            Assert.AreEqual(1, changes.ChangeList.Count);
            Assert.AreEqual(name, changes.ChangeList[0]);

            CreateTestFile(3);
            name = GetPathname();
            WaitWatcher();
            Assert.AreEqual(2, changes.ChangeList.Count);
            Assert.AreEqual(name, changes.ChangeList[1]);

            changes.EnableMonitorChanges = false;

            CreateTestFile(4);
            WaitWatcher();
            Assert.AreEqual(2, changes.ChangeList.Count);

            changes.EnableMonitorChanges = true;

            CreateTestFile(5);
            name = GetPathname();
            WaitWatcher();
            Assert.AreEqual(3, changes.ChangeList.Count);
            Assert.AreEqual(name, changes.ChangeList[2]);
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
            FileSystemChanges changes = new FileSystemChanges(TestFolder);
            changes.EnableMonitorChanges = true;

            List<string> names = new List<string>();
            for (int i = 0; i < NormalNumber; ++i)
            {
                CreateTestFile();
                names.Add(GetPathname());
            }
            WaitWatcher();
            Assert.AreEqual(NormalNumber, changes.ChangeList.Count);
            for (int i = 0; i < NormalNumber; ++i)
            {
                Assert.AreEqual(names[i], changes.ChangeList[i]);
                Assert.AreEqual(
                    FileSystemChanges.ChangeTypes.Created,
                    changes.GetChangeType((string)changes.ChangeList[i]));
            }
        }

        [Test, Category("Slow")]
        public void TestChangeTypeCreatedHeavy()
        {
            FileSystemChanges changes = new FileSystemChanges(TestFolder);
            changes.EnableMonitorChanges = true;

            for (int i = 0; i < HeavyNumber; ++i)
            {
                CreateTestFile(0, i / FileInFolderNumber);
            }
            WaitWatcher();
            int totalNumber = HeavyNumber + (HeavyNumber - 1) / FileInFolderNumber;
            Assert.AreEqual(changes.ChangeList.Count, totalNumber);
            int fileNumber = 0;
            for (int i = 0; i < changes.ChangeList.Count; ++i)
            {
                if (File.Exists(changes.ChangeList[i]))
                {
                    Assert.AreEqual(
                        changes.GetChangeType((string)changes.ChangeList[i]),
                        FileSystemChanges.ChangeTypes.Created);
                    ++fileNumber;
                }
            }
            Assert.AreEqual(fileNumber, HeavyNumber);
        }

        [Test, Category("Fast")]
        public void TestChangeTypeChanged()
        {
            FileSystemChanges changes = new FileSystemChanges(TestFolder);
            changes.EnableMonitorChanges = true;

            List<string> names = new List<string>();
            for (int i = 0; i < NormalNumber; ++i)
            {
                CreateTestFile();
                names.Add(GetPathname());
            }
            for (int i = 0; i < NormalNumber; ++i)
            {
                CreateTestFile(names[i],i+1);
                names.Add(GetPathname());
            }
            WaitWatcher();
            Assert.AreEqual(changes.ChangeList.Count, NormalNumber);
            for (int i = 0; i < NormalNumber; ++i)
            {
                Assert.AreEqual(names[i], changes.ChangeList[i]);
                Assert.AreEqual(
                    changes.GetChangeType((string)changes.ChangeList[i]),
                    FileSystemChanges.ChangeTypes.Changed);
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
            FileSystemChanges changes = new FileSystemChanges(TestFolder);
            changes.EnableMonitorChanges = true;

            List<string> names = new List<string>();
            for (int i = 0; i < NormalNumber; ++i)
            {
                CreateTestFile();
                names.Add(GetPathname());
                File.Delete(names[i]);
            }
            WaitWatcher();
            Assert.AreEqual(NormalNumber, changes.ChangeList.Count);
            for (int i = 0; i < NormalNumber; ++i)
            {
                Assert.AreEqual(names[i], changes.ChangeList[i]);
                Assert.AreEqual(
                    changes.GetChangeType((string)changes.ChangeList[i]),
                    FileSystemChanges.ChangeTypes.Deleted);
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
            FileSystemChanges changes = new FileSystemChanges(TestFolder);
            changes.EnableMonitorChanges = true;
            Assert.AreEqual(FileSystemChanges.ChangeTypes.None, changes.GetChangeType(GetPathname()));
        }

        [Test, Category("Fast")]
        public void TestChangeTypeForMove()
        {
            string oldnameOut = Path.Combine(TestFolderParent, "test.old");
            string newnameOut = Path.Combine(TestFolderParent, "test.new");
            string oldname = Path.Combine(TestFolder, "test.old");
            string newname = Path.Combine(TestFolder, "test.new");

            FileSystemChanges changes = new FileSystemChanges(TestFolder);
            changes.EnableMonitorChanges = true;
            CreateTestFile(oldname, 1);
            File.Move(oldname, newname);
            WaitWatcher();
            Assert.AreEqual(changes.ChangeList.Count, 2);
            Assert.AreEqual(changes.ChangeList[0], oldname);
            Assert.AreEqual(changes.ChangeList[1], newname);
            Assert.AreEqual(changes.GetChangeType(oldname), FileSystemChanges.ChangeTypes.Deleted);
            Assert.AreEqual(changes.GetChangeType(newname), FileSystemChanges.ChangeTypes.Created);
            File.Delete(newname);

            changes = new FileSystemChanges(TestFolder);
            changes.EnableMonitorChanges = true;
            CreateTestFile(oldnameOut, 1);
            File.Move(oldnameOut, newname);
            WaitWatcher();
            Assert.AreEqual(changes.ChangeList.Count, 1);
            Assert.AreEqual(changes.ChangeList[0], newname);
            Assert.AreEqual(changes.GetChangeType(newname), FileSystemChanges.ChangeTypes.Created);
            File.Delete(newname);

            changes = new FileSystemChanges(TestFolder);
            changes.EnableMonitorChanges = true;
            CreateTestFile(oldname, 1);
            File.Move(oldname, newnameOut);
            WaitWatcher();
            Assert.AreEqual(changes.ChangeList.Count, 1);
            Assert.AreEqual(changes.ChangeList[0], oldname);
            Assert.AreEqual(changes.GetChangeType(oldname), FileSystemChanges.ChangeTypes.Deleted);
            File.Delete(newnameOut);

            changes = new FileSystemChanges(TestFolder);
            changes.EnableMonitorChanges = true;
            CreateTestFile(oldnameOut, 1);
            File.Move(oldnameOut, newnameOut);
            WaitWatcher();
            Assert.AreEqual(changes.ChangeList.Count, 0);
            File.Delete(newnameOut);
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

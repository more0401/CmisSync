using System;
using System.IO;
using System.Collections.Generic;

using CmisSync.Lib.Sync;



namespace UnitTestProject1
{
    using NUnit.Framework;

    [TestFixture]
    public class UnitTest1
    {
        private static string testFolderParent = "e:\\";
        private static string testFolder = "e:\\test";
        private static int normalNumber = 10;
        private static int heavyNumber = 10000;
        private static int fileInFolderNumber = 1000;

        private static Object testLock = new Object();
        private static int testNumber = 0;

        [TestFixtureSetUp]
        public static void ClassInit()
        {
            if (Directory.Exists(testFolder))
            {
                Directory.Delete(testFolder, true);
            }
            System.Threading.Thread.Sleep(100);
            Directory.CreateDirectory(testFolder);
        }

        [SetUp]
        public void TestInit()
        {
        }

        [Test]
        public void TestGet0()
        {
            Assert.AreEqual(0, FileSystemChanges.Get0());
        }

        [Test]
        public void TestEnableMonitorChanges()
        {
            FileSystemChanges changes = new FileSystemChanges(testFolder);

            CreateTestFile(1);
            System.Threading.Thread.Sleep(10);
            Assert.AreEqual(changes.ChangeList.Count, 0);

            changes.EnableMonitorChanges = true;

            string name = GetPathname();
            CreateTestFile(2);
            System.Threading.Thread.Sleep(10);
            Assert.AreEqual(changes.ChangeList.Count, 1);
            Assert.AreEqual(name, changes.ChangeList[0]);

            name = GetPathname();
            CreateTestFile(3);
            System.Threading.Thread.Sleep(10);
            Assert.AreEqual(changes.ChangeList.Count, 2);
            Assert.AreEqual(name, changes.ChangeList[1]);

            changes.EnableMonitorChanges = false;

            CreateTestFile(4);
            System.Threading.Thread.Sleep(10);
            Assert.AreEqual(changes.ChangeList.Count, 2);

            changes.EnableMonitorChanges = true;

            name = GetPathname();
            CreateTestFile(5);
            System.Threading.Thread.Sleep(10);
            Assert.AreEqual(changes.ChangeList.Count, 3);
            Assert.AreEqual(name, changes.ChangeList[2]);
        }

        [Test]
        public void TestRemove()
        {
            Assert.Fail("TODO");
        }

        [Test]
        public void TestIgnore()
        {
            Assert.Fail("TODO");
        }

        [Test]
        public void TestChangeTypeCreated()
        {
            FileSystemChanges changes = new FileSystemChanges(testFolder);
            changes.EnableMonitorChanges = true;

            List<string> names = new List<string>();
            for (int i = 0; i < normalNumber; ++i)
            {
                names.Add(GetPathname());
                CreateTestFile();
            }
            System.Threading.Thread.Sleep(10);
            Assert.AreEqual(changes.ChangeList.Count, normalNumber);
            for (int i = 0; i < normalNumber; ++i)
            {
                Assert.AreEqual(names[i], changes.ChangeList[i]);
                Assert.AreEqual(
                    changes.GetChangeType((string)changes.ChangeList[i]),
                    FileSystemChanges.ChangeTypes.Created);
            }
        }

        [Test]
        public void TestChangeTypeCreatedHeavy()
        {
            FileSystemChanges changes = new FileSystemChanges(testFolder);
            changes.EnableMonitorChanges = true;

            for (int i = 0; i < heavyNumber; ++i)
            {
                CreateTestFile(0, i / fileInFolderNumber);
            }
            System.Threading.Thread.Sleep(10);
            int totalNumber = heavyNumber + (heavyNumber - 1) / fileInFolderNumber;
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
            Assert.AreEqual(fileNumber, heavyNumber);
        }

        [Test]
        public void TestChangeTypeChanged()
        {
            FileSystemChanges changes = new FileSystemChanges(testFolder);
            changes.EnableMonitorChanges = true;

            List<string> names = new List<string>();
            for (int i = 0; i < normalNumber; ++i)
            {
                names.Add(GetPathname());
                CreateTestFile();
            }
            for (int i = 0; i < normalNumber; ++i)
            {
                names.Add(GetPathname());
                CreateTestFile(names[i],i+1);
            }
            System.Threading.Thread.Sleep(10);
            Assert.AreEqual(changes.ChangeList.Count, normalNumber);
            for (int i = 0; i < normalNumber; ++i)
            {
                Assert.AreEqual(names[i], changes.ChangeList[i]);
                Assert.AreEqual(
                    changes.GetChangeType((string)changes.ChangeList[i]),
                    FileSystemChanges.ChangeTypes.Changed);
            }
        }

        [Test]
        public void TestChangeTypeChangedHeavy()
        {
            Assert.Fail("TODO");
        }

        [Test]
        public void TestChangeTypeDeleted()
        {
            FileSystemChanges changes = new FileSystemChanges(testFolder);
            changes.EnableMonitorChanges = true;

            List<string> names = new List<string>();
            for (int i = 0; i < normalNumber; ++i)
            {
                names.Add(GetPathname());
                CreateTestFile();
                File.Delete(names[i]);
            }
            System.Threading.Thread.Sleep(10);
            Assert.AreEqual(changes.ChangeList.Count, normalNumber);
            for (int i = 0; i < normalNumber; ++i)
            {
                Assert.AreEqual(names[i], changes.ChangeList[i]);
                Assert.AreEqual(
                    changes.GetChangeType((string)changes.ChangeList[i]),
                    FileSystemChanges.ChangeTypes.Deleted);
            }
        }

        [Test]
        public void TestChangeTypeDeleteHeavy()
        {
            Assert.Fail("TODO");
        }

        [Test]
        public void TestChangeTypeNone()
        {
            FileSystemChanges changes = new FileSystemChanges(testFolder);
            changes.EnableMonitorChanges = true;
            Assert.AreEqual(FileSystemChanges.ChangeTypes.None, changes.GetChangeType(GetPathname()));
        }

        [Test]
        public void TestChangeTypeForMove()
        {
            string oldnameOut = Path.Combine(testFolderParent, "test.old");
            string newnameOut = Path.Combine(testFolderParent, "test.new");
            string oldname = Path.Combine(testFolder, "test.old");
            string newname = Path.Combine(testFolder, "test.new");

            FileSystemChanges changes = new FileSystemChanges(testFolder);
            changes.EnableMonitorChanges = true;
            CreateTestFile(oldname, 1);
            File.Move(oldname, newname);
            System.Threading.Thread.Sleep(10);
            Assert.AreEqual(changes.ChangeList.Count, 2);
            Assert.AreEqual(changes.ChangeList[0], oldname);
            Assert.AreEqual(changes.ChangeList[1], newname);
            Assert.AreEqual(changes.GetChangeType(oldname), FileSystemChanges.ChangeTypes.Deleted);
            Assert.AreEqual(changes.GetChangeType(newname), FileSystemChanges.ChangeTypes.Created);
            File.Delete(newname);

            changes = new FileSystemChanges(testFolder);
            changes.EnableMonitorChanges = true;
            CreateTestFile(oldnameOut, 1);
            File.Move(oldnameOut, newname);
            System.Threading.Thread.Sleep(10);
            Assert.AreEqual(changes.ChangeList.Count, 1);
            Assert.AreEqual(changes.ChangeList[0], newname);
            Assert.AreEqual(changes.GetChangeType(newname), FileSystemChanges.ChangeTypes.Created);
            File.Delete(newname);

            changes = new FileSystemChanges(testFolder);
            changes.EnableMonitorChanges = true;
            CreateTestFile(oldname, 1);
            File.Move(oldname, newnameOut);
            System.Threading.Thread.Sleep(10);
            Assert.AreEqual(changes.ChangeList.Count, 1);
            Assert.AreEqual(changes.ChangeList[0], oldname);
            Assert.AreEqual(changes.GetChangeType(oldname), FileSystemChanges.ChangeTypes.Deleted);
            File.Delete(newnameOut);

            changes = new FileSystemChanges(testFolder);
            changes.EnableMonitorChanges = true;
            CreateTestFile(oldnameOut, 1);
            File.Move(oldnameOut, newnameOut);
            System.Threading.Thread.Sleep(10);
            Assert.AreEqual(changes.ChangeList.Count, 0);
            File.Delete(newnameOut);
        }

        [Test]
        public void TestChangeTypeForMoveHeavy()
        {
            Assert.Fail("TODO");
        }

        [Test]
        public void TestChangeTypeMix()
        {
            Assert.Fail("TODO");
        }

        [Test]
        public void TestChangeTypeMixHeavy()
        {
            Assert.Fail("TODO");
        }

        private string GetPathnameAndNext(int level)
        {
            int number = 0;
            lock (UnitTest1.testLock)
            {
                number = UnitTest1.testNumber;
                ++UnitTest1.testNumber;
            }
            return GetPathname(number, level);
        }

        private string GetPathname(int level = 0)
        {
            int number = 0;
            lock (UnitTest1.testLock)
            {
                number = testNumber;
            }
            return GetPathname(number, level);
        }

        private string GetPathname(int number,int level)
        {
            string pathname = UnitTest1.testFolder;
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

            using (FileStream stream = File.OpenWrite(GetPathnameAndNext(level)))
            {
                // Write random data
                for (int i = 0; i < sizeInKB; i++)
                {
                    random.NextBytes(data);
                    stream.Write(data, 0, data.Length);
                }
            }
        }
    }
}

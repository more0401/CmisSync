using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;


namespace CmisSync.Lib.Sync
{
    public class FileSystemChanges
    {
        /**
         * thread lock for <c>changeList</c> and <c>changes</c>
         */
        private Object changeLock = new Object();
        /**
         * the file/folder pathname (relative to <c>folder</c>) list for changes
         */
        private List<string> changeList = new List<string>();
        /**
         * supported change type
         * rename a file: <c>Deleted</c> for the old name, and <c>Created</c> for the new name
         */
        public enum ChangeTypes { None, Created, Changed, Deleted };
        /**
         * key is the element in <c>changeList</c>
         */
        private Dictionary<string,ChangeTypes> changes = new Dictionary<string,ChangeTypes>();

        public List<string> ChangeList
        {
            get { lock (changeLock) { return new List<string>(changeList); } }
        }

        public ChangeTypes GetChangeType(string name)
        {
            lock (changeLock)
            {
                ChangeTypes type;
                if (changes.TryGetValue(name, out type))
                {
                    return type;
                }
                else
                {
                    return ChangeTypes.None;
                }
            }
        }

        private Object enableMonitorChangesLock = new Object();
        private bool enableMonitorChanges = false;

        //TODO: set to false automatically if System.IO.FileSystemWatcher Error Event
        public bool EnableMonitorChanges
        {
            get
            {
                lock (enableMonitorChangesLock)
                {
                    return enableMonitorChanges;
                }
            }
            set
            {
                lock (enableMonitorChangesLock)
                {
                    watcher.EnableRaisingEvents = value;
                    enableMonitorChanges = value;
                }
            }
        }

        private FileSystemWatcher watcher;

        public FileSystemChanges(string folder)
        {
            watcher = new FileSystemWatcher();
            watcher.Path = Path.GetFullPath(folder);
            watcher.IncludeSubdirectories = true;
            watcher.InternalBufferSize = 4 * 1024 * 16;

            watcher.Error += new ErrorEventHandler(OnError);
            watcher.Created += new FileSystemEventHandler(OnCreated);
            watcher.Deleted += new FileSystemEventHandler(OnDeleted);
            watcher.Changed += new FileSystemEventHandler(OnChanged);
            watcher.Renamed += new RenamedEventHandler(OnRenamed);

            EnableMonitorChanges = false;
        }

        private void OnCreated(object source, FileSystemEventArgs e)
        {
            List<ChangeTypes> checks = new List<ChangeTypes>();
            checks.Add(ChangeTypes.Deleted);
            ChangeHandle(e.FullPath, ChangeTypes.Created, checks);
        }

        private void OnDeleted(object source, FileSystemEventArgs e)
        {
            List<ChangeTypes> checks = new List<ChangeTypes>();
            checks.Add(ChangeTypes.Created);
            checks.Add(ChangeTypes.Changed);
            ChangeHandle(e.FullPath, ChangeTypes.Deleted, checks);
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            List<ChangeTypes> checks = new List<ChangeTypes>();
            checks.Add(ChangeTypes.Created);
            checks.Add(ChangeTypes.Changed);
            ChangeHandle(e.FullPath, ChangeTypes.Changed, checks);
        }

        private void ChangeHandle(string name, ChangeTypes type, List<ChangeTypes> checks)
        {
            lock (changeLock)
            {
                Debug.Assert(name.StartsWith(watcher.Path));
                ChangeTypes oldType;
                if (changes.TryGetValue(name, out oldType))
                {
                    Debug.Assert(checks.Contains(oldType));
                    changeList.Remove(name);
                }
                changeList.Add(name);
                changes[name] = type;
            }
        }

        private void OnRenamed(object source, RenamedEventArgs e)
        {
            string oldname = e.OldFullPath;
            string newname = e.FullPath;
            if (oldname.StartsWith(watcher.Path))
            {
                FileSystemEventArgs eventDelete = new FileSystemEventArgs(WatcherChangeTypes.Deleted, Path.GetDirectoryName(oldname), Path.GetFileName(oldname));
                OnDeleted(source, eventDelete);
            }
            if (newname.StartsWith(watcher.Path))
            {
                FileSystemEventArgs eventCreate = new FileSystemEventArgs(WatcherChangeTypes.Created, Path.GetDirectoryName(newname), Path.GetFileName(newname));
                OnCreated(source, eventCreate);
            }
        }

        private void OnError(object source, ErrorEventArgs e)
        {
            Debug.Assert(false);
            //TODO
        }

        public void RemoveChange(string name)
        {
            //TODO
        }

        public void RemoveAll()
        {
            //TODO
        }

        public void IgnoreChangeType(string name, ChangeTypes type)
        {
            //TODO
        }

        public static int Get0()
        {
            return 0;
        }
    }
}

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using DotCMIS.Client;
using DotCMIS;
using DotCMIS.Client.Impl;
using DotCMIS.Exceptions;
using DotCMIS.Enums;
using System.ComponentModel;
using System.Collections;
using DotCMIS.Data.Impl;

using System.Net;
using CmisSync.Lib.Cmis;
using DotCMIS.Data;
using log4net;

namespace CmisSync.Lib.Sync
{
    public partial class CmisRepo : RepoBase
    {
        // Log.
        private static readonly ILog Logger = LogManager.GetLogger(typeof(CmisRepo));


        /// <summary>
        /// Synchronization with a particular CMIS folder.
        /// </summary>
        public partial class SynchronizedFolder : IDisposable
        {
            /// <summary>
            /// Whether sync is bidirectional or only from server to client.
            /// TODO make it a CMIS folder - specific setting
            /// </summary>
            private bool BIDIRECTIONAL = true;


            /// <summary>
            /// At which degree the repository supports Change Logs.
            /// See http://docs.oasis-open.org/cmis/CMIS/v1.0/os/cmis-spec-v1.0.html#_Toc243905424
            /// The possible values are actually none, objectidsonly, properties, all
            /// But for now we only distinguish between none (false) and the rest (true)
            /// </summary>
            private bool ChangeLogCapability;


            /// <summary>
            /// Session to the CMIS repository.
            /// </summary>
            private ISession session;


            /// <summary>
            /// Path of the root in the remote repository.
            // Example: "/User Homes/nicolas.raoul/demos"
            /// </summary>
            private string remoteFolderPath;


            /// <summary>
            /// Syncing lock.
            /// true if syncing is being performed right now.
            /// TODO use is_syncing variable in parent
            /// </summary>
            private bool syncing;


            /// <summary>
            /// Parameters to use for all CMIS requests.
            /// </summary>
            private Dictionary<string, string> cmisParameters;


            /// <summary>
            /// Track whether <c>Dispose</c> has been called.
            /// </summary>
            private bool disposed = false;


            /// <summary>
            /// Database to cache remote information from the CMIS server.
            /// </summary>
            private Database database;


            /// <summary>
            /// Listener we inform about activity (used by spinner).
            /// </summary>
            private IActivityListener activityListener;


            /// <summary>
            /// Configuration of the CmisSync synchronized folder, as defined in the XML configuration file.
            /// </summary>
            private RepoInfo repoinfo;


            /// <summary>
            /// Link to parent object.
            /// </summary>
            private RepoBase repo;


            /// <summary>
            ///  Constructor for Repo (at every launch of CmisSync)
            /// </summary>
            public SynchronizedFolder(RepoInfo repoInfo,
                IActivityListener listener, RepoBase repoCmis)
            {
                if (null == repoInfo || null == repoCmis)
                {
                    throw new ArgumentNullException("repoInfo");
                }

                this.repo = repoCmis;
                this.activityListener = listener;
                this.repoinfo = repoInfo;

                // Database is the user's AppData/Roaming
                database = new Database(repoinfo.CmisDatabase);

                // Get path on remote repository.
                remoteFolderPath = repoInfo.RemotePath;

                cmisParameters = new Dictionary<string, string>();
                cmisParameters[SessionParameter.BindingType] = BindingType.AtomPub;
                cmisParameters[SessionParameter.AtomPubUrl] = repoInfo.Address.ToString();
                cmisParameters[SessionParameter.User] = repoInfo.User;
                cmisParameters[SessionParameter.Password] = Crypto.Deobfuscate(repoInfo.Password); // Deobfuscate password.
                cmisParameters[SessionParameter.RepositoryId] = repoInfo.RepoID;
                cmisParameters[SessionParameter.ConnectTimeout] = "-1";

				foreach (string ignoredFolder in repoInfo.getIgnoredPaths())
				{
					Logger.Info("The folder \""+ignoredFolder+"\" will be ignored");
				}
            }


            /// <summary>
            /// Destructor.
            /// </summary>
            ~SynchronizedFolder()
            {
                Dispose(false);
            }


            /// <summary>
            /// Implement IDisposable interface. 
            /// </summary>
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }


            /// <summary>
            /// Dispose pattern implementation.
            /// </summary>
            protected virtual void Dispose(bool disposing)
            {
                if (!this.disposed)
                {
                    if (disposing)
                    {
                        this.database.Dispose();
                    }
                    this.disposed = true;
                }
            }


            /// <summary>
            /// Connect to the CMIS repository.
            /// </summary>
            public void Connect()
            {
                try
                {
                    // Create session factory.
                    SessionFactory factory = SessionFactory.NewInstance();
                    session = factory.CreateSession(cmisParameters);
                    // Detect whether the repository has the ChangeLog capability.
                    ChangeLogCapability = session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.All
                            || session.RepositoryInfo.Capabilities.ChangesCapability == CapabilityChanges.ObjectIdsOnly;
                    Logger.Info("ChangeLog capability: " + ChangeLogCapability.ToString());
                    Logger.Info("Created CMIS session: " + session.ToString());
                }
                catch (CmisRuntimeException e)
                {
                    Logger.Error("Connection to repository failed: ", e);
                }
            }


            /// <summary>
            /// Track whether a full sync is done
            /// </summary>
            private bool syncFull = false;

            /// <summary>
            /// Synchronize between CMIS folder and local folder.
            /// </summary>
            public void Sync()
            {
                // If not connected, connect.
                if (session == null)
                {
                    Connect();
                }
                if (session == null)
                {
                    Logger.Error("Could not connect to: " + cmisParameters[SessionParameter.AtomPubUrl]);
                    return; // Will try again at next sync. 
                }

                IFolder remoteFolder = (IFolder)session.GetObjectByPath(remoteFolderPath);
                string localFolder = repoinfo.TargetDirectory;

                if (!repo.Watcher.EnableRaisingEvents)
                {
                    repo.Watcher.RemoveAll();
                    repo.Watcher.EnableRaisingEvents = true;
                    syncFull = false;
                }

                if (!syncFull)
                {
                    Logger.Info("Invoke a full crawl sync");
                    syncFull = CrawlSync(remoteFolder, localFolder);
                    return;
                }

                if (ChangeLogCapability)
                {
                    Logger.Info("Invoke a remote change log sync");
                    ChangeLogSync(remoteFolder);
                }
                else
                {
                    //  have to crawl remote
                    Logger.Info("Invoke a remote crawl sync");
                    CrawlSync(remoteFolder, localFolder);
                }

                Logger.Info("Invoke a file system watcher sync");
                WatcherSync(remoteFolderPath, localFolder);
                foreach (string name in repo.Watcher.GetChangeList())
                {
                    Logger.Debug(String.Format("Change name {0} type {1}", name, repo.Watcher.GetChangeType(name)));
                }

            }


            /// <summary>
            /// Sync in the background.
            /// </summary>
            public void SyncInBackground()
            {
                if (this.syncing)
                {
                    //Logger.Debug("Sync already running in background: " + repoinfo.TargetDirectory);
                    return;
                }
                this.syncing = true;

                using (BackgroundWorker bw = new BackgroundWorker())
                {
                    bw.DoWork += new DoWorkEventHandler(
                        delegate(Object o, DoWorkEventArgs args)
                        {
                            Logger.Info("Launching sync: " + repoinfo.TargetDirectory);
#if !DEBUG
                        try
                        {
#endif
                            Sync();
#if !DEBUG
                        }
                        catch (CmisBaseException e)
                        {
                            Logger.Error("CMIS exception while syncing:", e);
                        }
#endif
                        }
                    );
                    bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
                        delegate(object o, RunWorkerCompletedEventArgs args)
                        {
                            this.syncing = false;
                        }
                    );
                    bw.RunWorkerAsync();
                }
            }


            /// <summary>
            /// Download all content from a CMIS folder.
            /// </summary>
            private bool RecursiveFolderCopy(IFolder remoteFolder, string localFolder)
            {
                activityListener.ActivityStarted();

                bool success = true;
                // List all children.
                foreach (ICmisObject cmisObject in remoteFolder.GetChildren())
                {
                    if (cmisObject is DotCMIS.Client.Impl.Folder)
                    {
                        IFolder remoteSubFolder = (IFolder)cmisObject;
                        string localSubFolder = localFolder + Path.DirectorySeparatorChar.ToString() + cmisObject.Name;
                        if (Utils.WorthSyncing(localSubFolder) && !repoinfo.isPathIgnored(remoteSubFolder.Path))
                        {
                            // Create local folder.
                            Directory.CreateDirectory(localSubFolder);

                            // Create database entry for this folder
                            // TODO Add metadata
                            database.AddFolder(localSubFolder, remoteSubFolder.Id, remoteSubFolder.LastModificationDate);

                            // Recurse into folder.
                            success = RecursiveFolderCopy(remoteSubFolder, localSubFolder) && success;
                        }
                    }
                    else
                    {
                        if (Utils.WorthSyncing(cmisObject.Name))
                            // It is a file, just download it.
                            success = DownloadFile((IDocument)cmisObject, localFolder) && success;
                    }
                }

                activityListener.ActivityStopped();

                return success;
            }


            /// <summary>
            /// Download a single folder from the CMIS server for sync.
            /// </summary>
            private bool SyncDownloadFolder(IFolder remoteSubFolder, string localFolder)
            {
                string name = remoteSubFolder.Name;
                string remotePathname = remoteSubFolder.Path;
                string localSubFolder = Path.Combine(localFolder, name);

                // If there was previously a file with this name, delete it.
                // TODO warn if local changes in the file.
                if (File.Exists(localSubFolder))
                {
                    File.Delete(localSubFolder);
                }

                if (Directory.Exists(localSubFolder))
                {
                    return true;
                }

                if (database.ContainsFolder(localSubFolder))
                {
                    // If there was previously a folder with this name, it means that
                    // the user has deleted it voluntarily, so delete it from server too.

                    // Delete the folder from the remote server.
                    remoteSubFolder.DeleteTree(true, null, true);

                    // Delete the folder from database.
                    database.RemoveFolder(localSubFolder);
                }
                else
                {
                    // The folder has been recently created on server, so download it.

                    // Skip if invalid folder name. See https://github.com/nicolas-raoul/CmisSync/issues/196
                    if (Utils.IsInvalidFolderName(name))
                    {
                        Logger.Info("Skipping download of folder with illegal name: " + name);
                    }
                    else if (repoinfo.isPathIgnored(remotePathname))
                    {
                        Logger.Info("Skipping dowload of ignored folder: " + remotePathname);
                    }
                    else
                    {
                        // Create local folder.remoteDocument.Name
                        Directory.CreateDirectory(localSubFolder);

                        // Create database entry for this folder.
                        // TODO - Yannick - Add metadata
                        database.AddFolder(localSubFolder, remoteSubFolder.Id, remoteSubFolder.LastModificationDate);
                    }
                }

                return true;
            }


            /// <summary>
            /// Download a single file from the CMIS server for sync.
            /// </summary>
            private bool SyncDownloadFile(IDocument remoteDocument, string localFolder, IList remoteFiles = null)
            {
                string name = remoteDocument.Name;
                // We use the filename of the document's content stream.
                // This can be different from the name of the document.
                // For instance in FileNet it is not usual to have a document where
                // document.Name is "foo" and document.ContentStreamFileName is "foo.jpg".
                string fileName = remoteDocument.ContentStreamFileName;
                string filePath = Path.Combine(localFolder, fileName);

                // If this file does not have a filename, ignore it.
                // It sometimes happen on IBM P8 CMIS server, not sure why.
                if (fileName == null)
                {
                    Logger.Warn("Skipping download of '" + name + "' with null content stream in " + localFolder);
                    return true;
                }

                if (!Utils.WorthSyncing(fileName))
                {
                    Logger.Info("Ignore the unworth syncing remote file: " + fileName);
                    return true;
                }

                // Check if file extension is allowed

                if (null != remoteFiles)
                {
                    remoteFiles.Add(fileName);
                }

                bool success = true;

                if (File.Exists(filePath))
                {
                    // Check modification date stored in database and download if remote modification date if different.
                    DateTime? serverSideModificationDate = ((DateTime)remoteDocument.LastModificationDate).ToUniversalTime();
                    DateTime? lastDatabaseUpdate = database.GetServerSideModificationDate(filePath);

                    if (lastDatabaseUpdate == null)
                    {
                        Logger.Info("Downloading file absent from database: " + filePath);
                        success = DownloadFile(remoteDocument, localFolder);
                    }
                    else
                    {
                        // If the file has been modified since last time we downloaded it, then download again.
                        if (serverSideModificationDate > lastDatabaseUpdate)
                        {
                            if (database.LocalFileHasChanged(filePath))
                            {
                                Logger.Info("Conflict with file: " + fileName + ", backing up locally modified version and downloading server version");
                                // Rename locally modified file.
                                String ext = Path.GetExtension(filePath);
                                String filename = Path.GetFileNameWithoutExtension(filePath);
                                String dir = Path.GetDirectoryName(filePath);

                                String newFileName = Utils.SuffixIfExists(Path.GetFileNameWithoutExtension(filePath) + "_" + repoinfo.User + "-version");
                                String newFilePath = Path.Combine(dir, newFileName);
                                File.Move(filePath, newFilePath);

                                // Download server version
                                success = DownloadFile(remoteDocument, localFolder);
                                repo.OnConflictResolved();

                                // TODO move to OS-dependant layer
                                //System.Windows.Forms.MessageBox.Show("Someone modified a file at the same time as you: " + filePath
                                //    + "\n\nYour version has been saved with a '_your-version' suffix, please merge your important changes from it and then delete it.");
                                // TODO show CMIS property lastModifiedBy
                            }
                            else
                            {
                                Logger.Info("Downloading modified file: " + fileName);
                                success = DownloadFile(remoteDocument, localFolder);
                            }
                        }

                    }
                }
                else
                {
                    if (database.ContainsFile(filePath))
                    {
                        // File has been recently removed locally, so remove it from server too.
                        Logger.Info("Removing locally deleted file on server: " + filePath);
                        remoteDocument.DeleteAllVersions();
                        // Remove it from database.
                        database.RemoveFile(filePath);
                    }
                    else
                    {
                        // New remote file, download it.
                        Logger.Info("New remote file: " + filePath);
                        success = DownloadFile(remoteDocument, localFolder);
                    }
                }

                return success;
            }


            /// <summary>
            /// Download a single file from the CMIS server.
            /// </summary>
            private bool DownloadFile(IDocument remoteDocument, string localFolder)
            {
                activityListener.ActivityStarted();

                string fileName = remoteDocument.ContentStreamFileName;
                Logger.Info("Downloading: " + fileName);

                // TODO: Make this configurable.
                if (remoteDocument.ContentStreamLength == 0)
                {
                    Logger.Info("Skipping download of file with content length zero: " + fileName);
                    activityListener.ActivityStopped();
                    return true;
                }

                // Skip if invalid file name. See https://github.com/nicolas-raoul/CmisSync/issues/196
                if (Utils.IsInvalidFileName(fileName))
                {
                    Logger.Info("Skipping download of file with illegal filename: " + fileName);
                    activityListener.ActivityStopped();
                    return true;
                }

                try
                {
                    DotCMIS.Data.IContentStream contentStream = null;
                    string filepath = Path.Combine(localFolder, fileName);
                    string tmpfilepath = filepath + ".sync";

                    // If there was previously a directory with this name, delete it.
                    // TODO warn if local changes inside the folder.
                    if (Directory.Exists(filepath))
                    {
                        Directory.Delete(filepath);
                    }

                    // If file exists, delete it.
                    File.Delete(filepath);
                    File.Delete(tmpfilepath);

                    // Download file.
                    Boolean success = false;
                    try
                    {
                        contentStream = remoteDocument.GetContentStream();

                        // If this file does not have a content stream, ignore it.
                        // Even 0 bytes files have a contentStream.
                        // null contentStream sometimes happen on IBM P8 CMIS server, not sure why.
                        if (contentStream == null)
                        {
                            Logger.Warn("Skipping download of file with null content stream: " + fileName);
                            activityListener.ActivityStopped();
                            return true;
                        }

                        DownloadStream(contentStream, tmpfilepath);

                        contentStream.Stream.Close();
                        success = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Download failed: " + fileName + " " + ex);
                        if (contentStream != null) contentStream.Stream.Close();
                        success = false;
                        File.Delete(tmpfilepath);
                    }

                    if (success)
                    {
                        Logger.Info("Downloaded: " + fileName);
                        // TODO Control file integrity by using hash compare?

                        // Get metadata.
                        Dictionary<string, string[]> metadata = null;
                        try
                        {
                            metadata = FetchMetadata(remoteDocument);
                        }
                        catch (Exception e)
                        {
                            Logger.Info("Exception while fetching metadata: " + fileName + " " + Utils.ToLogString(e));
                            // Remove temporary local document to avoid it being considered a new document.
                            File.Delete(tmpfilepath);
                            activityListener.ActivityStopped();
                            return false;
                        }

                        // Remove the ".sync" suffix.
                        File.Move(tmpfilepath, filepath);

                        // Create database entry for this file.
                        database.AddFile(filepath, remoteDocument.Id, remoteDocument.LastModificationDate, metadata);

                        Logger.Info("Added to database: " + fileName);
                    }

                    activityListener.ActivityStopped();
                    return success;
                }
                catch (IOException e)
                {
                    Logger.Warn("Exception while file operation: " + Utils.ToLogString(e));
                    activityListener.ActivityStopped();
                    return false;
                }
            }


            /// <summary>
            /// Download a file, without retrying.
            /// </summary>
            private void DownloadStream(DotCMIS.Data.IContentStream contentStream, string filePath)
            {
                using (Stream file = File.OpenWrite(filePath))
                {
                    byte[] buffer = new byte[8 * 1024];
                    int len;
                    while ((len = contentStream.Stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        file.Write(buffer, 0, len);
                    }
                }
                contentStream.Stream.Close();
            }


            /// <summary>
            /// Upload a single file to the CMIS server.
            /// </summary>
            private bool UploadFile(string filePath, IFolder remoteFolder)
            {
                activityListener.ActivityStarted();

                IDocument remoteDocument = null;
                Boolean success = false;
                try
                {
                    Logger.Info("Uploading: " + filePath);

                    // Prepare properties
                    string fileName = Path.GetFileName(filePath);
                    Dictionary<string, object> properties = new Dictionary<string, object>();
                    properties.Add(PropertyIds.Name, fileName);
                    properties.Add(PropertyIds.ObjectTypeId, "cmis:document");

                    // Prepare content stream
                    using (Stream file = File.OpenRead(filePath))
                    {
                        ContentStream contentStream = new ContentStream();
                        contentStream.FileName = fileName;
                        contentStream.MimeType = MimeType.GetMIMEType(fileName);
                        contentStream.Length = file.Length;
                        contentStream.Stream = file;

                        // Upload
                        try
                        {
                            remoteDocument = remoteFolder.CreateDocument(properties, contentStream, null);
                            success = true;
                        }
                        catch (Exception ex)
                        {
                            Logger.Fatal("Upload failed: " + filePath + " " + ex);
                        }
                    }
                }
                catch (Exception e)
                {
                    if (e is FileNotFoundException ||
                        e is IOException)
                    {
                        Logger.Warn("File deleted while trying to upload it, reverting.");
                        // File has been deleted while we were trying to upload/checksum/add.
                        // This can typically happen in Windows Explore when creating a new text file and giving it a name.
                        // In this case, revert the upload.
                        if (remoteDocument != null)
                        {
                            remoteDocument.DeleteAllVersions();
                        }
                    }
                    else
                    {
                        //throw;
                    }
                }
                    
                // Metadata.
                if (success)
                {
                    Logger.Info("Uploaded: " + filePath);

                    // Get metadata. Some metadata has probably been automatically added by the server.
                    Dictionary<string, string[]> metadata = FetchMetadata(remoteDocument);

                    // Create database entry for this file.
                    database.AddFile(filePath, remoteDocument.Id, remoteDocument.LastModificationDate, metadata);
                }

                activityListener.ActivityStopped();
                return success;
            }


            /// <summary>
            /// Upload folder recursively.
            /// After execution, the hierarchy on server will be: .../remoteBaseFolder/localFolder/...
            /// </summary>
            private bool UploadFolderRecursively(IFolder remoteBaseFolder, string localFolder)
            {
                // Create remote folder.
                Dictionary<string, object> properties = new Dictionary<string, object>();
                properties.Add(PropertyIds.Name, Path.GetFileName(localFolder));
                properties.Add(PropertyIds.ObjectTypeId, "cmis:folder");
                IFolder folder = remoteBaseFolder.CreateFolder(properties);

                // Create database entry for this folder
                // TODO Add metadata
                database.AddFolder(localFolder, folder.Id, folder.LastModificationDate);

                bool success = true;
                try
                {
                    // Upload each file in this folder.
                    foreach (string file in Directory.GetFiles(localFolder))
                    {
                        if (Utils.WorthSyncing(file))
                        {
                            success = UploadFile(file, folder) && success;
                        }
                    }

                    // Recurse for each subfolder in this folder.
                    foreach (string subfolder in Directory.GetDirectories(localFolder))
                    {
                        string path = subfolder.Substring(repoinfo.TargetDirectory.Length);
                        path = path.Replace("\\\\","/");
                        if (Utils.WorthSyncing(subfolder) && !repoinfo.isPathIgnored(path))
                        {
                            success = UploadFolderRecursively(folder, subfolder) && success;
                        }
                    }
                }
                catch (Exception e)
                {
                    if (e is System.IO.DirectoryNotFoundException ||
                        e is IOException)
                    {
                        Logger.Warn("Folder deleted while trying to upload it, reverting.");
                        // Folder has been deleted while we were trying to upload/checksum/add.
                        // In this case, revert the upload.
                        folder.DeleteTree(true, null, true);
                    }
                    else
                    {
                        return false;
                    }
                }

                return success;
            }


            /// <summary>
            /// Upload new version of file.
            /// </summary>
            private bool UpdateFile(string filePath, IDocument remoteFile)
            {
                try
                {
                    Logger.Info("## Updating " + filePath);
                    using (Stream localfile = File.OpenRead(filePath))
                    {
                        // Ignore files with null or empty content stream.
                        if ((localfile == null) && (localfile.Length == 0))
                        {
                            Logger.Info("Skipping update of file with null or empty content stream: " + filePath);
                            return true;
                        }

                        // Prepare content stream
                        ContentStream remoteStream = new ContentStream();
                        remoteStream.FileName = remoteFile.ContentStreamFileName;
                        remoteStream.Length = localfile.Length;
                        remoteStream.MimeType = MimeType.GetMIMEType(Path.GetFileName(filePath));
                        remoteStream.Stream = localfile;
                        remoteStream.Stream.Flush();
                        Logger.Debug("before SetContentStream");

                        // CMIS do not have a Method to upload block by block. So upload file must be full.
                        // We must waiting for support of CMIS 1.1 https://issues.apache.org/jira/browse/CMIS-628
                        // http://docs.oasis-open.org/cmis/CMIS/v1.1/cs01/CMIS-v1.1-cs01.html#x1-29700019
                        // DotCMIS.Client.IObjectId objID = remoteFile.SetContentStream(remoteStream, true, true);
                        remoteFile.SetContentStream(remoteStream, true, true);

                        Logger.Debug("after SetContentStream");
                        Logger.Info("## Updated " + filePath);
                        return true;
                    }
                }
                catch (Exception e)
                {
                    Logger.Warn(String.Format("Exception while update file {0}: {1}", filePath, e));
                    return false;
                }
            }

            /// <summary>
            /// Upload new version of file content.
            /// </summary>
            private bool UpdateFile(string filePath, IFolder remoteFolder)
            {
                Logger.Info("# Updating " + filePath);

                // Tell the tray icon to start spinning.
                activityListener.ActivityStarted();
                
                // Find the document within the folder.
                string fileName = Path.GetFileName(filePath);
                IDocument document = null;
                bool found = false;
                foreach (ICmisObject obj in remoteFolder.GetChildren())
                {
                    if (null != (document = obj as IDocument))
                    {
                        if (document.Name == fileName)
                        {
                            found = true;
                            break;
                        }
                    }
                }

                // If not found, it means the document has been deleted.
                if (!found)
                {
                    Logger.Info(filePath + " not found on server, must be uploaded instead of updated");
                    return UploadFile(filePath, remoteFolder);
                }

                // Update the document itself.
                bool success = UpdateFile(filePath, document);

                if (success)
                {
                    // Update timestamp in database.
                    database.SetFileServerSideModificationDate(filePath, ((DateTime)document.LastModificationDate).ToUniversalTime());

                    // Update checksum
                    database.RecalculateChecksum(filePath);

                    // TODO Update metadata?
                }

                // Tell the tray icon to stop spinning.
                activityListener.ActivityStopped();

                Logger.Info("# Updated " + filePath);

                return success;
            }


            /// <summary>
            /// Move folder from local filesystem and database.
            /// </summary>
            private void MoveFolderLocally(string oldFolderPath, string newFolderPath)
            {
                if (!Directory.Exists(oldFolderPath))
                {
                    return;
                }

                if (!Directory.Exists(newFolderPath))
                {
                    Directory.Move(oldFolderPath, newFolderPath);
                    database.MoveFolder(oldFolderPath, newFolderPath);
                    return;
                }

                foreach (FileInfo file in new DirectoryInfo(oldFolderPath).GetFiles())
                {
                    string oldFilePath = Path.Combine(oldFolderPath, file.Name);
                    string newFilePath = Path.Combine(newFolderPath, file.Name);
                    if (File.Exists(newFilePath))
                    {
                        File.Delete(oldFilePath);
                        database.RemoveFile(oldFilePath);
                    }
                    else
                    {
                        File.Move(oldFilePath, newFilePath);
                        database.MoveFile(oldFilePath, newFilePath);
                    }
                }

                foreach (DirectoryInfo folder in new DirectoryInfo(oldFolderPath).GetDirectories())
                {
                    MoveFolderLocally(Path.Combine(oldFolderPath, folder.Name), Path.Combine(newFolderPath, folder.Name));
                }

                return;
            }


            /// <summary>
            /// Remove folder from local filesystem and database.
            /// </summary>
            private bool RemoveFolderLocally(string folderPath)
            {
                // Folder has been deleted on server, delete it locally too.
                try
                {
                    Logger.Info("Removing remotely deleted folder: " + folderPath);
                    Directory.Delete(folderPath, true);
                }
                catch (IOException e)
                {
                    Logger.Warn(String.Format("Exception while delete tree {0}: {1}", folderPath, Utils.ToLogString(e)));
                    return false;
                }

                // Delete folder from database.
                if (!Directory.Exists(folderPath))
                {
                    database.RemoveFolder(folderPath);
                }

                return true;
            }

            /// <summary>
            /// Retrieve the CMIS metadata of a document.
            /// </summary>
            /// <returns>a dictionary in which each key is a type id and each value is a couple indicating the mode ("readonly" or "ReadWrite") and the value itself.</returns>
            private Dictionary<string, string[]> FetchMetadata(IDocument document)
            {
                Dictionary<string, string[]> metadata = new Dictionary<string, string[]>();

                IObjectType typeDef = session.GetTypeDefinition(document.ObjectType.Id/*"cmis:document" not Name FullName*/); // TODO cache
                IList<IPropertyDefinition> propertyDefs = typeDef.PropertyDefinitions;

                // Get metadata.
                foreach (IProperty property in document.Properties)
                {
                    // Mode
                    string mode = "readonly";
                    foreach (IPropertyDefinition propertyDef in propertyDefs)
                    {
                        if (propertyDef.Id.Equals("cmis:name"))
                        {
                            Updatability updatability = propertyDef.Updatability;
                            mode = updatability.ToString();
                        }
                    }

                    // Value
                    if (property.IsMultiValued)
                    {
                        metadata.Add(property.Id, new string[] { property.DisplayName, mode, property.ValuesAsString });
                    }
                    else
                    {
                        metadata.Add(property.Id, new string[] { property.DisplayName, mode, property.ValueAsString });
                    }
                }

                return metadata;
            }
        }
    }
}

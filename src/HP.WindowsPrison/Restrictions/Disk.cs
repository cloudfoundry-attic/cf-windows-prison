using Alphaleonis.Win32.Filesystem;
using DiskQuotaTypeLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HP.WindowsPrison.Restrictions
{
    class Disk : Rule
    {
        public static class DiskQuotaManager
        {
            /// <summary>
            /// DiskQuotaControlls mapped to the unique volume name.
            /// </summary>
            private static Dictionary<string, DIDiskQuotaControl> quotaControls = new Dictionary<string, DIDiskQuotaControl>();

            private static object locker = new object();

            /// <summary>
            /// Initialize the quota for every volume on the system.
            /// </summary>
            public static void StartQuotaInitialization()
            {
                var systemVolumes = Volume.EnumerateVolumes();

                lock (locker)
                {
                    foreach (string volume in systemVolumes)
                    {
                        try
                        {
                            var volumeInfo = Volume.GetVolumeInfo(volume);
                            if (volumeInfo.VolumeQuotas)
                            {
                                StartQuotaInitialization(volume);
                            }
                        }
                        catch (DeviceNotReadyException)
                        {
                        }
                    }
                }
            }

            public static void StartQuotaInitialization(string volumeUniqueName)
            {
                lock (locker)
                {
                    if (!quotaControls.ContainsKey(volumeUniqueName))
                    {
                        var qcontrol = quotaControls[volumeUniqueName] = new DiskQuotaControlClass();

                        qcontrol.Initialize(volumeUniqueName, true);
                        qcontrol.QuotaState = QuotaStateConstants.dqStateEnforce;

                        // Set to ResolveNone to prevent blocking when using account names.
                        qcontrol.UserNameResolution = UserNameResolutionConstants.dqResolveNone;
                        qcontrol.LogQuotaThreshold = true;
                        qcontrol.LogQuotaLimit = true;

                        // Disable default quota limit and threshold
                        qcontrol.DefaultQuotaThreshold = -1;
                        qcontrol.DefaultQuotaLimit = -1;
                    }
                }
            }

            public static bool IsQuotaInitialized()
            {
                lock (locker)
                {
                    foreach (var uniqueVolumeName in quotaControls.Keys)
                    {
                        if (!IsQuotaInitialized(uniqueVolumeName))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }

            public static bool IsQuotaInitialized(string uniqueVolumeName)
            {
                lock (locker)
                {
                   DIDiskQuotaControl qcontrol = null;

                    if (quotaControls.TryGetValue(uniqueVolumeName, out qcontrol))
                    {
                        return !qcontrol.QuotaFileIncomplete && !qcontrol.QuotaFileRebuilding;
                    }
                }
                return false;
            }

            /// <summary>
            /// Gets a object that manages the quota for a specific user on a specific volume.
            /// </summary>
            /// <param name="rootPath"></param>
            /// <param name="WindowsUsername"></param>
            public static DIDiskQuotaUser GetDiskQuotaUser(string rootPath, string WindowsUsername)
            {
                lock (locker)
                {
                    var uniqueVolumeName = Volume.GetUniqueVolumeNameForPath(rootPath);

                    DIDiskQuotaControl qcontrol = null;

                    if (quotaControls.TryGetValue(uniqueVolumeName, out qcontrol))
                    {
                        return qcontrol.AddUser(WindowsUsername);
                    }

                    throw new ArgumentException("Volume root path not found or not initialized. ", "rootPath");
                }
            }

            /// <summary>
            /// Gets a objects that manages the quota for a specific user on all the system's volumes.
            /// </summary>
            /// <param name="windowsUsername"></param>
            public static DIDiskQuotaUser[] GetDisksQuotaUser(string windowsUsername)
            {
                lock (locker)
                {
                    var res = new List<DIDiskQuotaUser>();
                    foreach (var qcontrol in quotaControls.Values)
                    {
                        res.Add(qcontrol.AddUser(windowsUsername));
                    }

                    return res.ToArray();
                }
            }

            public static void SetDiskQuotaLimit(string WindowsUsername, string Path, long DiskQuotaBytes)
            {
                var rootPath = DiskQuotaManager.GetVolumeRootFromPath(Path);
                var userQuota = DiskQuotaManager.GetDiskQuotaUser(rootPath, WindowsUsername);
                userQuota.QuotaLimit = DiskQuotaBytes;
            }

            /// <summary>
            /// Get the volume root mount of the path. 
            /// </summary>
            /// <param name="path">The path for which the volume should be returned.</param>
            /// <returns>The root volume path.</returns>
            public static string GetVolumeRootFromPath(string path)
            {
                string currentPath = path.EndsWith(@"\", StringComparison.Ordinal) ? path : path + @"\";
                bool isVolume = Alphaleonis.Win32.Filesystem.Volume.IsVolume(currentPath);

                if (isVolume)
                {
                    return currentPath;
                }
                else
                {
                    string parentPath = new System.IO.DirectoryInfo(path).Parent.FullName;
                    return GetVolumeRootFromPath(parentPath);
                }
            }

            /// <summary>
            /// Get the unique volume name for a path. 
            /// </summary>
            /// <param name="path">The path for which the volume should be returned.</param>
            public static string GetUniqueVolumeNameFromPath(string path)
            {
                return Volume.GetUniqueVolumeNameForPath(GetVolumeRootFromPath(path));
            }
        }

        public override void Apply(Prison prison)
        {
            if (prison == null)
            {
                throw new ArgumentNullException("prison");
            }

            // Set the disk quota to 0 for all disks, except disk quota path
            var volumesQuotas = GetUserQoutaDiskQuotaManager(prison);

            foreach (var volumeQuota in volumesQuotas)
            {
                volumeQuota.QuotaLimit = 0;
            }

            DiskQuotaManager.SetDiskQuotaLimit(prison.User.UserName, prison.Configuration.PrisonHomePath, prison.Configuration.DiskQuotaBytes);
        }

        public override void Destroy(Prison prison)
        {
        }

        public override void Init()
        {
            DiskQuotaManager.StartQuotaInitialization();

            while (!DiskQuotaManager.IsQuotaInitialized())
            {
                Thread.Sleep(200);
            }
        }

        public override RuleInstanceInfo[] List()
        {
            return new RuleInstanceInfo[0];
        }

        public override RuleTypes RuleType
        {
            get
            {
                return RuleTypes.Disk;
            }
        }
        public override void Recover(Prison prison)
        {
        }

        private static DIDiskQuotaUser[] GetUserQoutaDiskQuotaManager(Prison prison)
        {
            return DiskQuotaManager.GetDisksQuotaUser(prison.User.UserName);
        }
    }
}

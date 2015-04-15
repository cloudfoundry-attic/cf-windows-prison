namespace HP.WindowsPrison
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Runtime.Serialization;

    public static class PrisonManager
    {
        private const string DatabaseDirectoryName = @"windows-prison-db";
        private static readonly string databaseDirectory = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), PrisonManager.DatabaseDirectoryName));

        /// <summary>
        /// Loads all persisted Prison instances.
        /// <remarks>
        /// Prison objects are stored in the system folder, in a directory named 'windows-prison-db'.
        /// This is usually 'c:\windows\system32\windows-prison-db'.
        /// </remarks>
        /// </summary>
        /// <returns>An array of Prison objects.</returns>
        public static Prison[] ReadAllPrisonsNoAttach()
        {
            List<Prison> result = new List<Prison>();

            Logger.Debug("Loading prison database from {0}", databaseDirectory);

            Directory.CreateDirectory(databaseDirectory);

            string[] prisonFiles = Directory.GetFiles(databaseDirectory, "*.xml", SearchOption.TopDirectoryOnly);

            Logger.Debug("Found {0} prison entries", prisonFiles.Length);

            DataContractSerializer serializer = new DataContractSerializer(typeof(Prison));

            foreach (string prisonLocation in prisonFiles)
            {
                using (FileStream readStream = File.OpenRead(prisonLocation))
                {
                    Prison loadedPrison = (Prison)serializer.ReadObject(readStream);
                    result.Add(loadedPrison);
                }
            }

            return result.ToArray();
        }

        public static Prison LoadPrisonAndAttach(Guid prisonId)
        {
            Prison loadedPrison = LoadPrisonNoAttach(prisonId);

            if (loadedPrison != null)
            {
                loadedPrison.Reattach();
                return loadedPrison;
            }
            else
            {
                return null;
            }
        }

        public static Prison LoadPrisonNoAttach(Guid prisonId)
        {
            string prisonFilePath = GetPrisonFileName(prisonId);
            if (!File.Exists(prisonFilePath))
            {
                return null;
            }
            else
            {
                DataContractSerializer serializer = new DataContractSerializer(typeof(Prison));

                using (FileStream readStream = File.OpenRead(prisonFilePath))
                {
                   return (Prison)serializer.ReadObject(readStream);
                }
            }
        }

        public static void Save(Prison prison)
        {
            if (prison == null)
            {
                throw new ArgumentNullException("prison");
            }

            Logger.Debug("Persisting prison {0}", prison.Id);

            Directory.CreateDirectory(databaseDirectory);

            string fileName = GetPrisonFileName(prison);

            DataContractSerializer serializer = new DataContractSerializer(typeof(Prison));

            using (FileStream writeStream = File.Open(fileName, FileMode.Create, FileAccess.Write))
            {
                serializer.WriteObject(writeStream, prison);
            }
        }

        public static void DeletePersistedPrison(Prison prison)
        {
            if (prison == null)
            {
                throw new ArgumentNullException("prison");
            }

            Logger.Debug("Deleting persisted prison {0}", prison.Id);

            Directory.CreateDirectory(databaseDirectory);

            string fileName = GetPrisonFileName(prison);

            File.Delete(fileName);
        }

        private static string GetPrisonFileName(Prison prison)
        {
            if (prison == null)
            {
                throw new ArgumentNullException("prison");
            }

            return GetPrisonFileName(prison.Id);
        }

        private static string GetPrisonFileName(Guid prisonId)
        {
            return Path.Combine(databaseDirectory, string.Format(CultureInfo.InvariantCulture, "{0}.xml", prisonId.ToString("N")));
        }
    }
}

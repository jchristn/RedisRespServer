namespace Redish.Server.Settings
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Storage settings.
    /// </summary>
    public class StorageSettings
    {
        /// <summary>
        /// In-memory storage.  Data is not persisted across restarts.
        /// </summary>
        public StorageModeEnum Mode { get; set; } = StorageModeEnum.Ram;

        /// <summary>
        /// Storage settings.
        /// </summary>
        public StorageSettings()
        {

        }
    }
}
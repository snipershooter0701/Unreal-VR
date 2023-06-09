﻿using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Security.Cryptography;
using Windows.Storage;

namespace UnrealVR
{
    public class ProfileModel : INotifyPropertyChanged
    {
        public ProfileModel()
        {
            Filename = Guid.NewGuid().ToString() + ".json";
            SaveToAppData();
        }

        /**
         * Versions:
         * 1. Initial
         * 2. Added FOV multiplier
         * 
         * Currently using v1 because ProjectionMatrix calculations were fixed
         */
        public ProfileModel(string filename, dynamic model)
        {
            Filename = filename;
            if (model._Version == 1 || model._Version == 2)
            {
                Name = model.Name;
                ShippingExe = model.ShippingExe;
                CommandLineArgs = model.CommandLineArgs;
                CmUnitsScale = model.CmUnitsScale;
                UsesFChunkedFixedUObjectArray = model.UsesFChunkedFixedUObjectArray;
                Uses422NamePool = model.Uses422NamePool;
                UsesFNamePool = model.UsesFNamePool;
                UsesDeferredSpawn = model.UsesDeferredSpawn;
            }
        }

        public string Filename { get; set; }
        public string Name { get; set; } = "Untitled";
        public string ShippingExe { get; set; } = "";
        public string CommandLineArgs { get; set; } = "";
        public float CmUnitsScale { get; set; } = 1.0f;
        public bool UsesFChunkedFixedUObjectArray { get; set; } = false;
        public bool Uses422NamePool { get; set; } = false;
        public bool UsesFNamePool { get; set; } = false;
        public bool UsesDeferredSpawn { get; set; } = false;

        private Task RunningSaveTask;
        private bool IsSaveTaskQueued = false;

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /** Uses a pseudo-mutex to prevent race conditions when writing profiles; only ever queues up to one save task, which will only save the latest version of this profile. */
        public async void SaveToAppData()
        {
            if (IsSaveTaskQueued) return;
            IsSaveTaskQueued = true;
            if (RunningSaveTask != null && !RunningSaveTask.IsCompleted) await RunningSaveTask;
            var localFolder = ApplicationData.Current.LocalFolder;
            var profileFolder = await localFolder.CreateFolderAsync("UnrealVRProfiles", CreationCollisionOption.OpenIfExists);
            StorageFile profileFile = await profileFolder.CreateFileAsync(Filename, CreationCollisionOption.OpenIfExists);
            IsSaveTaskQueued = false;
            RunningSaveTask = SaveToFile(profileFile);
            await RunningSaveTask;
        }

        /** Doesn't use a mutex because we don't expect the user to manually export their config at a high frequency */
        public async Task SaveToFile(StorageFile profileFile)
        {
            dynamic model = new
            {
                _Version = 1,
                Name,
                ShippingExe,
                CommandLineArgs,
                CmUnitsScale,
                UsesFChunkedFixedUObjectArray,
                Uses422NamePool,
                UsesFNamePool,
                UsesDeferredSpawn
            };
            string text = JsonConvert.SerializeObject(model);
            var buffer = CryptographicBuffer.ConvertStringToBinary(text, BinaryStringEncoding.Utf8);
            await FileIO.WriteBufferAsync(profileFile, buffer);
        }

        public ProfileModel Clone() => new()
        {
            Name = Name + " (Copy)",
            ShippingExe = ShippingExe,
            CommandLineArgs = CommandLineArgs,
            CmUnitsScale = CmUnitsScale,
            UsesFChunkedFixedUObjectArray = UsesFChunkedFixedUObjectArray,
            Uses422NamePool = Uses422NamePool,
            UsesFNamePool = UsesFNamePool,
            UsesDeferredSpawn = UsesDeferredSpawn
        };

        public async Task Delete()
        {
            var localFolder = ApplicationData.Current.LocalFolder;
            var profileFolder = await localFolder.CreateFolderAsync("UnrealVRProfiles", CreationCollisionOption.OpenIfExists);
            StorageFile profileFile = await profileFolder.CreateFileAsync(Filename, CreationCollisionOption.ReplaceExisting);
            await profileFile.DeleteAsync();
        }

        private static readonly string UML_PROFILE_FORMAT = @"# An UnrealModLoader profile for {0} auto-generated by UnrealVR
[GameInfo]
IsUsingFChunkedFixedUObjectArray={1}
IsUsing4_22={2}
UsesFNamePool={3}
IsUsingDeferedSpawn={4}";
        private static readonly string UML_CONFIG_FORMAT = @"# An UnrealModLoader config auto-generated by UnrealVR
[DEBUG]
UseConsole={0}";

        public async Task WriteUMLProfile()
        {
            var name = ShippingExe.Split('\\')[^1][..^4];
            var filename = name + ".profile";
            var localFolder = ApplicationData.Current.LocalFolder;
            var profileFolder = await localFolder.CreateFolderAsync("UnrealModLoaderProfiles", CreationCollisionOption.OpenIfExists);
            var profileFile = await profileFolder.CreateFileAsync(filename, CreationCollisionOption.OpenIfExists);
            var profileContent = string.Format(
                UML_PROFILE_FORMAT,
                name,
                UsesFChunkedFixedUObjectArray ? "1" : "0",
                Uses422NamePool ? "1" : "0",
                UsesFNamePool ? "1" : "0",
                UsesDeferredSpawn ? "1" : "0"
            );
            var profileBuffer = CryptographicBuffer.ConvertStringToBinary(profileContent, BinaryStringEncoding.Utf8);
            await FileIO.WriteBufferAsync(profileFile, profileBuffer);
            var configFile = await localFolder.CreateFileAsync("ModLoaderInfo.ini", CreationCollisionOption.OpenIfExists);
            var configContent = string.Format(UML_CONFIG_FORMAT, "1"); // TODO: Enable/disable UML console
            var configBuffer = CryptographicBuffer.ConvertStringToBinary(configContent, BinaryStringEncoding.Utf8);
            await FileIO.WriteBufferAsync(configFile, configBuffer);
        }
    }
}

﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Resources;
using xivModdingFramework.SqPack.FileTypes;

namespace xivModdingFramework.Models.FileTypes
{
    public class Eqp
    {
        public const string EquipmentParameterExtension = "edp";
        public const string EquipmentParameterFile = "chara/xls/equipmentparameter/equipmentparameter.eqp";
        public const string EquipmentDeformerParameterExtension = "eqdp";
        public const string EquipmentDeformerParameterRootPath = "chara/xls/charadb/equipmentdeformerparameter/";
        public const string AccessoryDeformerParameterRootPath = "chara/xls/charadb/accessorydeformerparameter/";

        private readonly DirectoryInfo _gameDirectory;
        private readonly DirectoryInfo _modListDirectory;

        // Full EQP entries are 8 bytes long.
        public const int EquipmentParameterEntrySize = 8;

        // Full EQDP entries are 2 bytes long.
        public const int EquipmentDeformerParameterEntrySize = 2;
        public const int EquipmentDeformerParameterHeaderLength = 320;


        // The subset list of races that actually have deformation files.
        public static readonly List<XivRace> DeformationAvailableRaces = new List<XivRace>()
        {
            XivRace.Hyur_Midlander_Male,
            XivRace.Hyur_Midlander_Female,
            XivRace.Hyur_Highlander_Male,
            XivRace.Hyur_Highlander_Female,
            XivRace.Elezen_Male,
            XivRace.Elezen_Female,
            XivRace.Miqote_Male,
            XivRace.Miqote_Female,
            XivRace.Roegadyn_Male,
            XivRace.Roegadyn_Female,
            XivRace.Lalafell_Male,
            XivRace.Lalafell_Female,
            XivRace.AuRa_Male,
            XivRace.AuRa_Female,
            XivRace.Hrothgar,
            XivRace.Viera,
        };

        private Dat _dat;

        public Eqp(DirectoryInfo gameDirectory)
        {
            _gameDirectory = gameDirectory;
            _dat = new Dat(_gameDirectory);
            _modListDirectory = new DirectoryInfo(Path.Combine(gameDirectory.Parent.Parent.FullName, XivStrings.ModlistFilePath));
        }

        public async Task<EquipmentParameterSet> GetEquipmentParameters(int equipmentId)
        {
            throw new NotImplementedException("Not Yet Implemented.");
        }

        /// <summary>
        /// Saves the given Equipment Parameter information to the main EQP file for the given set.
        /// </summary>
        /// <param name="setId"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task SaveEqpEntry(string pathWithOffset, EquipmentParameter data)
        {

            var match = _eqpBinaryOffsetRegex.Match(pathWithOffset);
            if (!match.Success) throw new InvalidDataException("Invalid EQP Path: " + pathWithOffset);

            var bitOffset = Int32.Parse(match.Groups[1].Value);
            var byteOffset = bitOffset / 8;

            var setId = byteOffset / EquipmentParameterEntrySize;
            var slotOffset = byteOffset % EquipmentParameterEntrySize;

            var slotKv = EquipmentParameterSet.EntryOffsets.Reverse().First(x => x.Value <= slotOffset);
            var slot = slotKv.Key;
            var slotByteOffset = slotKv.Value;

            var size = EquipmentParameterSet.EntrySizes[slot];

            var offset = (setId * EquipmentParameterEntrySize) + slotByteOffset;

            var file = (await LoadEquipmentParameterFile(false)).ToList();

            if (offset + size >= file.Count) throw new InvalidDataException("Invalid EQP Offset: " + pathWithOffset);

            var bytes = data.GetBytes();

            IOUtil.ReplaceBytesAt(file, bytes, offset);

            await _dat.ImportType2Data(file.ToArray(), "_EQP_INTERNAL_", EquipmentParameterFile, Constants.InternalMetaFileSourceName, Constants.InternalMetaFileSourceName);
        }

        private static readonly Regex _eqpBinaryOffsetRegex = new Regex(Constants.BinaryOffsetMarker + "([0-9]+)$");
        public async Task<EquipmentParameter> GetEqpEntry(string pathWithOffset, bool forceDefault = false)
        {
            var match = _eqpBinaryOffsetRegex.Match(pathWithOffset);
            if (!match.Success) return null;

            var bitOffset = Int32.Parse(match.Groups[1].Value);
            var byteOffset = bitOffset / 8;

            var setId = byteOffset / EquipmentParameterEntrySize;
            var slotOffset = byteOffset % EquipmentParameterEntrySize;

            var slotKv = EquipmentParameterSet.EntryOffsets.Reverse().First(x => x.Value <= slotOffset);
            var slot = slotKv.Key;
            var slotByteOffset = slotKv.Value;

            var size = EquipmentParameterSet.EntrySizes[slot];

            var offset = (setId * EquipmentParameterEntrySize) + slotByteOffset;

            var file = await LoadEquipmentParameterFile(forceDefault);

            if (offset + size >= file.Length) return null;

            var bytes = file.Skip(offset).Take(size);

            return new EquipmentParameter(slot, bytes.ToArray());

        }

        /// <summary>
        /// Get the raw bytes for the equipment parameters for a given equipment set.
        /// </summary>
        /// <param name="equipmentId"></param>
        /// <returns></returns>
        public async Task<BitArray> GetRawEquipmentParameters(int equipmentId)
        {
            var data = await LoadEquipmentParameterFile();
            var start = (equipmentId * EquipmentParameterEntrySize);
            var parameters = new byte[EquipmentParameterEntrySize];


            // This item doesn't have equipment parameters.
            if(start >= data.Length)
            {
                return null;
            }

            // 8 Bytes
            for (var idx = 0; idx < EquipmentParameterEntrySize; idx++)
            {
                parameters[idx] = data[start + idx];
            }

            return new BitArray(parameters);
        }

        /// <summary>
        /// Gets the raw equipment parameter file.
        /// </summary>
        /// <returns></returns>
        private async Task<byte[]> LoadEquipmentParameterFile(bool forceDefault = false)
        {
            return await _dat.GetType2Data(EquipmentParameterFile, forceDefault);
        }


        public async Task SaveRawEquipmentParameters(int equipmentId, BitArray entry) {
            var file = new List<byte>(await _dat.GetType2Data(EquipmentParameterFile, false)).ToArray();


            var start = (equipmentId * EquipmentParameterEntrySize);
            entry.CopyTo(file, start);

            await SaveEquipmentParameterFile(file);


        }

        private async Task SaveEquipmentParameterFile(byte[] file)
        {

            await _dat.ImportType2Data(file, "EquipmentParameterFile", EquipmentParameterFile, "Internal", "TexTools");

            return;
        }




        private static readonly Regex _eqdpBinaryOffsetRegex = new Regex("^(.*c([0-9]{4}).*)" + Constants.BinaryOffsetMarker + "([0-9]+)$");


        public async Task SaveEqdpEntries(uint primaryId, string slot, Dictionary<XivRace, EquipmentDeformationParameter> parameters)
        {
            var isAccessory = EquipmentDeformationParameterSet.SlotsAsList(true).Contains(slot);

            if (!isAccessory)
            {
                var slotOk = EquipmentDeformationParameterSet.SlotsAsList(false).Contains(slot);
                if (!slotOk)
                {
                    throw new InvalidDataException("Attempted to save racial models for invalid slot.");
                }
            }

            var original = new Dictionary<XivRace, EquipmentDeformationParameter>();
            foreach (var race in DeformationAvailableRaces)
            {
                var set = await GetEquipmentDeformationSet((int)primaryId, race, isAccessory);
                original.Add(race, set.Parameters[slot]);
            }

            var _index = new Index(_gameDirectory);
            var _mdl = new Mdl(_gameDirectory, XivDataFile._04_Chara);

            foreach(var race in DeformationAvailableRaces)
            {
                if(original.ContainsKey(race) && parameters.ContainsKey(race))
                {
                    if(parameters[race].bit1 && !original[race].bit1 )
                    {
                        // If we're adding a new race, we need to clone an existing model, if it doesn't exist already.
                        var path = "";
                        if (!isAccessory)
                        {
                            path = String.Format(_EquipmentModelPathFormat, primaryId.ToString().PadLeft(4, '0'), race.GetRaceCode(), slot);
                        }
                        else
                        {
                            path = String.Format(_AccessoryModelPathFormat, primaryId.ToString().PadLeft(4, '0'), race.GetRaceCode(), slot);
                        }

                        // File already exists, no adjustments needed.
                        if ((await _index.FileExists(path))) continue;

                        var baseModelOrder = race.GetModelPriorityList();

                        // Ok, we need to find which racial model to use as our base now...
                        var baseRace = XivRace.All_Races;
                        foreach(var targetRace in baseModelOrder)
                        {
                            if(original.ContainsKey(targetRace) && original[targetRace].bit1 == true)
                            {
                                baseRace = targetRace;
                                break;
                            }
                        }

                        if (baseRace == XivRace.All_Races) throw new Exception("Unable to find base model to create new racial model from.");
                        var originalPath = "";
                        if (!isAccessory)
                        {
                            originalPath = String.Format(_EquipmentModelPathFormat, primaryId.ToString().PadLeft(4, '0'), baseRace.GetRaceCode(), slot);
                        }
                        else
                        {
                            originalPath = String.Format(_AccessoryModelPathFormat, primaryId.ToString().PadLeft(4, '0'), baseRace.GetRaceCode(), slot);
                        }


                        var exists = await _index.FileExists(originalPath);
                        if (!exists) throw new Exception("Base file for model-copy does not exist: " + originalPath);

                        // Create the new model.
                        await _mdl.CopyModel(originalPath, path);
                    }
                }
            }



            // 16 Bits per set.
            uint bitOffset = (primaryId * (EquipmentDeformerParameterEntrySize * 8)) + (EquipmentDeformerParameterHeaderLength * 8);

            // 2 Bits per slot entry.
            uint slotOffset = (uint)(EquipmentDeformationParameterSet.SlotsAsList(isAccessory).IndexOf(slot) * 2);

            bitOffset += slotOffset;
            uint byteOffset = bitOffset / 8;

            foreach(var race in DeformationAvailableRaces)
            {
                // Don't change races we weren't given information for.
                if (!parameters.ContainsKey(race)) continue;
                var entry = parameters[race];

                var rootPath = isAccessory ? AccessoryDeformerParameterRootPath : EquipmentDeformerParameterRootPath;
                var fileName = rootPath + "c" + race.GetRaceCode() + "." + EquipmentDeformerParameterExtension;

                // Load the file and flip the bits as needed.
                var file = await LoadEquipmentDeformationFile(race, isAccessory, false);

                var byteToModify = file[(int)byteOffset];

                var bitshift = (int)(slotOffset % 8);
                
                if(entry.bit0)
                {
                    byteToModify = (byte)(byteToModify | (1 << bitshift));
                } else
                {
                    byteToModify = (byte)(byteToModify & ~(1 << bitshift));
                }

                if (entry.bit1)
                {
                    byteToModify = (byte)(byteToModify | (1 << (bitshift + 1)));
                }
                else
                {
                    byteToModify = (byte)(byteToModify & ~(1 << (bitshift + 1)));
                }

                file[(int)byteOffset] = byteToModify;

                await _dat.ImportType2Data(file.ToArray(), "_EQDP_INTERNAL_", fileName, Constants.InternalMetaFileSourceName, Constants.InternalMetaFileSourceName);
            }
        }

        /// <summary>
        /// Retrieves the raw EQDP entries from an arbitrary selection of files/offsets.
        /// </summary>
        /// <param name="pathsWithOffsets"></param>
        /// <returns></returns>
        public async Task<Dictionary<XivRace, EquipmentDeformationParameter>> GetEqdpEntries(List<string> pathsWithOffsets, bool forceDefault = false)
        {
            var ret = new Dictionary<XivRace, EquipmentDeformationParameter>();
            foreach(var path in pathsWithOffsets)
            {
                var match = _eqdpBinaryOffsetRegex.Match(path);

                // Invalid format.
                if (!match.Success) continue;

                var file = match.Groups[1].Value;
                var race = XivRaces.GetXivRace(match.Groups[2].Value);
                var bitOffset = Int32.Parse(match.Groups[3].Value);

                // 2 Bytes per set.
                int setId = (bitOffset - (EquipmentDeformerParameterHeaderLength * 8)) / (EquipmentDeformerParameterEntrySize * 8);

                var slotOffset = (bitOffset % (EquipmentDeformerParameterEntrySize * 8)) / EquipmentDeformerParameterEntrySize;
                var accessory = file.Contains("accessory");
                var list = EquipmentDeformationParameterSet.SlotsAsList(accessory);
                var slot = list[slotOffset];

                var set = await GetEquipmentDeformationSet(setId, race, accessory, forceDefault);


                if(set == null)
                {
                    // Either invalid race, or this item doesn't use EQDP sets.
                    continue;
                }

                ret.Add(race, set.Parameters[slot]);

            }
            return ret;
        }

        /// <summary>
        /// Get all the available models for a given piece of equipment.
        /// </summary>
        /// <param name="equipmentId"></param>
        /// <param name="accessory"></param>
        /// <returns></returns>
        public async Task<List<XivRace>> GetAvailableRacialModels(int equipmentId, string slot)
        {
            var isAccessory = EquipmentDeformationParameterSet.SlotsAsList(true).Contains(slot);

            if(!isAccessory)
            {
                var slotOk = EquipmentDeformationParameterSet.SlotsAsList(false).Contains(slot);
                if(!slotOk)
                {
                    throw new InvalidDataException("Attempted to get racial models for invalid slot.");
                }
            }

            var sets = await GetAllEquipmentDeformationSets(equipmentId, isAccessory);
            var races = new List<XivRace>();

            if (sets != null)
            {
                foreach (var kv in sets)
                {
                    var race = kv.Key;
                    var set = kv.Value;
                    var entry = set.Parameters[slot];

                    // Bit0 has unknown purpose currently.
                    if (entry.bit1)
                    {
                        races.Add(race);
                    }
                }
            } else
            {
                var _index = new Index(_gameDirectory);

                // Ok, at this point we're in a somewhat unusual item; it's an item that is effectively non-set.
                // It has an item set ID in the multiple thousands (ex. 5000/9000), so it does not use the EQDP table.
                // In these cases, there's nothing to do but hard check the model paths, until such a time as we know
                // how these are resolved.
                foreach (var race in DeformationAvailableRaces)
                {
                    var path = "";
                    if (!isAccessory)
                    {
                        path = String.Format(_EquipmentModelPathFormat, equipmentId.ToString().PadLeft(4, '0'), race.GetRaceCode(), slot);
                    }
                    else
                    {
                        path = String.Format(_AccessoryModelPathFormat, equipmentId.ToString().PadLeft(4, '0'), race.GetRaceCode(), slot);
                    }
                    if(await _index.FileExists(path))
                    {
                        races.Add(race);
                    }
                }

            }

            return races;
        }

        /// <summary>
        /// Gets all of the equipment or accessory deformation sets for a given equipment id.
        /// </summary>
        /// <param name="equipmentId"></param>
        /// <param name="accessory"></param>
        /// <returns></returns>
        private async Task<Dictionary<XivRace, EquipmentDeformationParameterSet>> GetAllEquipmentDeformationSets(int equipmentId, bool accessory)
        {
            var sets = new Dictionary<XivRace, EquipmentDeformationParameterSet>();

            foreach (var race in DeformationAvailableRaces)
            {
                var result = await GetEquipmentDeformationSet(equipmentId, race, accessory);
                if (result != null) {
                    sets.Add(race, result);
                } else
                {
                    return null;
                }
            }

            return sets;
        }



        /// <summary>
        /// Get the equipment or accessory deformation set for a given item and race.
        /// Null if the set information doesn't exist.
        /// </summary>
        /// <param name="equipmentId"></param>
        /// <param name="race"></param>
        /// <param name="accessory"></param>
        /// <returns></returns>
        private async Task<EquipmentDeformationParameterSet> GetEquipmentDeformationSet(int equipmentId, XivRace race, bool accessory = false, bool forceDefault = false)
        {
            var raw = await GetRawEquipmentDeformationParameters(equipmentId, race, accessory, forceDefault);
            if(raw == null)
            {
                return null;
            }

            var set = new EquipmentDeformationParameterSet(accessory);

            var list = EquipmentDeformationParameterSet.SlotsAsList(accessory);

            // Pull the value apart two bits at a time.
            // Last 6 bits are not used.
            for (var idx = 0; idx < 5; idx++)
            {
                var entry = new EquipmentDeformationParameter();

                entry.bit0 = (raw & 1) != 0;
                raw = (ushort)(raw >> 1);
                entry.bit1 = (raw & 1) != 0;
                raw = (ushort)(raw >> 1);

                var key = list[idx];
                set.Parameters[key] = entry;
            }


            return set;
        }

        private string _EquipmentModelPathFormat = "chara/equipment/e{0}/model/c{1}e{0}_{2}.mdl";
        private string _AccessoryModelPathFormat = "chara/equipment/a{0}/model/c{1}a{0}_{2}.mdl";
        /// <summary>
        /// Get the raw bytes for the equipment or accessory deformation parameters for a given equipment set and race.
        /// </summary>
        /// <param name="equipmentId"></param>
        /// <param name="race"></param>
        /// <returns></returns>
        private async Task<ushort?> GetRawEquipmentDeformationParameters(int equipmentId, XivRace race, bool accessory = false, bool forceDefault = false)
        {
            var data = await LoadEquipmentDeformationFile(race, accessory, forceDefault);
            var start = EquipmentDeformerParameterHeaderLength + (equipmentId * EquipmentDeformerParameterEntrySize);
            var parameters = new byte[EquipmentParameterEntrySize];

            if(start >= data.Count)
            {

                return null;
            }

            for (var idx = 0; idx < EquipmentDeformerParameterEntrySize; idx++)
            {
                parameters[idx] = data[start + idx];
            }

            return BitConverter.ToUInt16(parameters, 0);
        }

        /// <summary>
        /// Gets the raw equipment or accessory deformation parameters file for a given race.
        /// </summary>
        /// <returns></returns>
        private async Task<List<byte>> LoadEquipmentDeformationFile(XivRace race, bool accessory = false, bool forceDefault = false)
        {
            var rootPath = accessory ? AccessoryDeformerParameterRootPath : EquipmentDeformerParameterRootPath;
            var fileName = rootPath + "c" + race.GetRaceCode() + "." + EquipmentDeformerParameterExtension;
            return new List<byte>(await _dat.GetType2Data(fileName, forceDefault));
        }

    }
}

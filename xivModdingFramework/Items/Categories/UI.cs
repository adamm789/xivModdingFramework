// xivModdingFramework
// Copyright © 2018 Rafael Gonzalez - All Rights Reserved
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using xivModdingFramework.Cache;
using xivModdingFramework.Exd.Enums;
using xivModdingFramework.Exd.FileTypes;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.HUD.FileTypes;
using xivModdingFramework.Items.DataContainers;
using xivModdingFramework.Resources;
using xivModdingFramework.Mods;
using static xivModdingFramework.Exd.FileTypes.Ex;

namespace xivModdingFramework.Items.Categories
{
    /// <summary>
    /// This class contains getters for different types of UI elements
    /// </summary>
    public class UI
    {
        private readonly DirectoryInfo _gameDirectory;
        private readonly XivLanguage _xivLanguage;
        private readonly Ex _ex;

        public UI(DirectoryInfo gameDirectory, XivLanguage xivLanguage)
        {
            _gameDirectory = gameDirectory;
            _xivLanguage = xivLanguage;
            _ex = new Ex(_gameDirectory, _xivLanguage);
        }


        public async Task<List<XivUi>> GetUIList()
        {
            return await XivCache.GetCachedUiList();
        }

        /// <summary>
        /// Gets a list of UI elements from the Uld Files
        /// </summary>
        /// <remarks>
        /// The uld files are in the 06 files
        /// They contain refrences to textures among other unknown things (likely placement data)
        /// </remarks>
        /// <returns>A list containing XivUi data</returns>
        public async Task<List<XivUi>> GetUldList(ModTransaction tx = null)
        {
            var uldLock = new object();
            var uldList = new List<XivUi>();

            var uld = new Uld(_gameDirectory);
            var uldPaths = await uld.GetTexFromUld(tx);

            await Task.Run(() => Parallel.ForEach(uldPaths, (uldPath) =>
            {
                var xivUi = new XivUi
                {
                    Name = Path.GetFileNameWithoutExtension(uldPath),
                    PrimaryCategory = "UI",
                    SecondaryCategory = XivStrings.HUD,
                    UiPath = "ui/uld"
                };

                if (xivUi.Name.Equals(string.Empty)) return;

                lock (uldLock)
                {
                    uldList.Add(xivUi);
                }
            }));

            uldList.Sort();

            return uldList;
        }

        private string GetPlaceName(Dictionary<int, ExdRow> placeData, object placeId)
        {
            return GetPlaceName(placeData, (ushort)placeId);
        }
        private string GetPlaceName(Dictionary<int, ExdRow> placeData, int placeId)
        {
            return (string) placeData[placeId].GetColumnByName("Name");
        }
        private string GetActionCategory(Dictionary<int, ExdRow> data, int index)
        {
            return (string) data[index].GetColumnByName("Name");
        }

        /// <summary>
        /// Gets the list of available map data
        /// </summary>
        /// <remarks>
        /// The map data is obtained from the map exd files
        /// There may be unlisted maps which this does not check for
        /// </remarks>
        /// <returns>A list containing XivUi data</returns>
        public async Task<List<XivUi>> GetMapList()
        {
            var mapLock = new object();
            var mapList = new List<XivUi>();

            var placeNameData = await _ex.ReadExData(XivEx.placename);
            var mapData = await _ex.ReadExData(XivEx.map);


            // Loops through all available maps in the map exd files
            // At present only one file exists (map_0)
            await Task.Run(() => Parallel.ForEach(mapData.Values, (map) =>
            {


                var regionName = GetPlaceName(placeNameData, map.GetColumnByName("RegionPlaceNameId"));
                var primaryName = GetPlaceName(placeNameData, map.GetColumnByName("PrimaryPlaceNameId"));
                var subMapName = GetPlaceName(placeNameData, map.GetColumnByName("SubPlaceNameId"));
                var mapId = (string) map.GetColumnByName("MapId");

                if (string.IsNullOrWhiteSpace(mapId))
                    return;

                var name = string.IsNullOrEmpty(subMapName) ? primaryName : subMapName;

                if (string.IsNullOrWhiteSpace(regionName))
                {
                    name = "Unknown Map - " + mapId;
                }
                else if (string.IsNullOrWhiteSpace(primaryName))
                {
                    name = "Unknown " + regionName + " Map " + mapId;
                }

                var xivUi = new XivUi()
                {
                    PrimaryCategory = "UI",
                    SecondaryCategory = XivStrings.Maps,
                    UiPath = mapId,
                    TertiaryCategory = regionName,
                    MapZoneCategory = string.IsNullOrEmpty(subMapName) ? "" : primaryName,
                    Name = name,
                };

                lock (mapLock)
                {
                    mapList.Add(xivUi);
                }
            }));

            mapList.Sort();

            return mapList;
        }

        /// <summary>
        /// Gets the list of action UI elements
        /// </summary>
        /// <remarks>
        /// The actions are obtained from different sources, but is not all inclusive
        /// There may be some actions that are missing
        /// </remarks>
        /// <returns>A list containing XivUi data</returns>
        public async Task<List<XivUi>> GetActionList()
        {
            var actionLock = new object();

            // Data from the action_0 exd
            var actionExData = await _ex.ReadExData(XivEx.action);
            var actionCategoryExData = await _ex.ReadExData(XivEx.actioncategory);

            var actionList = new List<XivUi>();
            var actionNames = new List<string>();

            await Task.Run(() => Parallel.ForEach(actionExData.Values, (action) =>
            {

                var name = (string) action.GetColumnByName("Name");
                var iconId = (ushort)action.GetColumnByName("Icon");
                var actionCatId = (byte)action.GetColumnByName("ActionCategoryId");
                var actionCat = GetActionCategory(actionCategoryExData, actionCatId);

                var xivUi = new XivUi()
                {
                    PrimaryCategory = "UI",
                    SecondaryCategory = XivStrings.Actions,
                    Name = name,
                    IconNumber = iconId,
                    TertiaryCategory = string.IsNullOrWhiteSpace(actionCat) ? XivStrings.None : actionCat
                };

                // The Cure icon is used as a placeholder so filter out all actions that aren't Cure but are using its icon as a placeholder
                if (string.IsNullOrWhiteSpace(xivUi.Name) || (!xivUi.Name.Equals("Cure") && xivUi.IconNumber == 405)) return;
                if (actionNames.Contains(xivUi.Name)) return;

                lock (actionLock)
                {
                    actionNames.Add(xivUi.Name);
                    actionList.Add(xivUi);
                }
             }));

            // Data from generalaction_0
            var generalActionExData = await _ex.ReadExData(XivEx.generalaction);

            await Task.Run(() => Parallel.ForEach(generalActionExData.Values, (action) =>
            {
                    var xivUi = new XivUi()
                    {
                        PrimaryCategory = "UI",
                        SecondaryCategory = XivStrings.Actions,
                        TertiaryCategory = XivStrings.General
                    };
                return;
                /*

                    // Big Endian Byte Order 
                    using (var br = new BinaryReaderBE(new MemoryStream(action)))
                    {
                        br.BaseStream.Seek(6, SeekOrigin.Begin);

                        var nameLength = br.ReadInt16();

                        br.BaseStream.Seek(10, SeekOrigin.Begin);

                        var iconNumber = br.ReadUInt16();

                        // Filter out any actions using placeholder icons
                        if (iconNumber == 0 || iconNumber == 405) return;

                        br.BaseStream.Seek(20, SeekOrigin.Begin);

                        var name = Encoding.UTF8.GetString(br.ReadBytes(nameLength)).Replace("\0", "");

                        xivUi.Name = name;
                        xivUi.IconNumber = iconNumber;
                    }

                    if (xivUi.Name.Equals(string.Empty)) return;

                    lock (actionLock)
                    {
                        actionNames.Add(xivUi.Name);
                        actionList.Add(xivUi);
                    }
                }));

                // Data from buddyaction_0
                var buddyActionExData = await _ex.ReadExData(XivEx.buddyaction);

                await Task.Run(() => Parallel.ForEach(buddyActionExData.Values, (action) =>
                {
                    var xivUi = new XivUi()
                    {
                        PrimaryCategory = "UI",
                        SecondaryCategory = XivStrings.Actions,
                        TertiaryCategory = XivStrings.Buddy
                    };

                    // Big Endian Byte Order 
                    using (var br = new BinaryReaderBE(new MemoryStream(action)))
                    {
                        br.BaseStream.Seek(6, SeekOrigin.Begin);

                        var nameLength = br.ReadInt16();

                        br.BaseStream.Seek(10, SeekOrigin.Begin);

                        var iconNumber = br.ReadUInt16();

                        // Filter out any actions using placeholder icons
                        if (iconNumber == 0 || iconNumber == 405) return;

                        br.BaseStream.Seek(20, SeekOrigin.Begin);

                        var name = Encoding.UTF8.GetString(br.ReadBytes(nameLength)).Replace("\0", "");

                        xivUi.Name = name;
                        xivUi.IconNumber = iconNumber;
                    }

                    if (xivUi.Name.Equals(string.Empty)) return;

                    lock (actionLock)
                    {
                        actionNames.Add(xivUi.Name);
                        actionList.Add(xivUi);
                    }
                }));

                // Data from companyaction_0
                var companyActionExData = await _ex.ReadExData(XivEx.companyaction);

                await Task.Run(() => Parallel.ForEach(companyActionExData.Values, (action) =>
                {
                    var xivUi = new XivUi()
                    {
                        PrimaryCategory = "UI",
                        SecondaryCategory = XivStrings.Actions,
                        TertiaryCategory = XivStrings.Company
                    };

                    // Big Endian Byte Order 
                    using (var br = new BinaryReaderBE(new MemoryStream(action)))
                    {
                        br.BaseStream.Seek(6, SeekOrigin.Begin);

                        var nameLength = br.ReadInt16();

                        br.BaseStream.Seek(14, SeekOrigin.Begin);

                        var iconNumber = br.ReadUInt16();

                        // Filter out any actions using placeholder icons
                        if (iconNumber == 0 || iconNumber == 405) return;

                        br.BaseStream.Seek(20, SeekOrigin.Begin);

                        var name = Encoding.UTF8.GetString(br.ReadBytes(nameLength)).Replace("\0", "");

                        xivUi.Name = name;
                        xivUi.IconNumber = iconNumber;
                    }

                    if (xivUi.Name.Equals(string.Empty)) return;

                    lock (actionLock)
                    {
                        actionNames.Add(xivUi.Name);
                        actionList.Add(xivUi);
                    }
                }));

                // Data from craftaction_100000
                var craftActionExData = await _ex.ReadExData(XivEx.craftaction);

                await Task.Run(() => Parallel.ForEach(craftActionExData.Values, (action) =>
                {
                    var xivUi = new XivUi()
                    {
                        PrimaryCategory = "UI",
                        SecondaryCategory = XivStrings.Actions,
                        TertiaryCategory = XivStrings.Craft
                    };

                    // Big Endian Byte Order 
                    using (var br = new BinaryReaderBE(new MemoryStream(action)))
                    {
                        br.BaseStream.Seek(6, SeekOrigin.Begin);

                        var nameLength = br.ReadInt16();

                        br.BaseStream.Seek(48, SeekOrigin.Begin);

                        var iconNumber = br.ReadUInt16();

                        // Filter out any actions using placeholder icons
                        if (iconNumber == 0 || iconNumber == 405) return;

                        br.BaseStream.Seek(60, SeekOrigin.Begin);

                        var name = Encoding.UTF8.GetString(br.ReadBytes(nameLength)).Replace("\0", "");

                        xivUi.Name = name;
                        xivUi.IconNumber = iconNumber;
                    }

                    if (xivUi.Name.Equals(string.Empty)) return;

                    lock (actionLock)
                    {
                        actionNames.Add(xivUi.Name);
                        actionList.Add(xivUi);
                    }
                }));

                // Data from eventaction_0
                var eventActionExData = await _ex.ReadExData(XivEx.eventaction);

                await Task.Run(() => Parallel.ForEach(eventActionExData.Values, (action) =>
                {
                    var xivUi = new XivUi()
                    {
                        PrimaryCategory = "UI",
                        SecondaryCategory = XivStrings.Actions,
                        TertiaryCategory = XivStrings.Event
                    };

                    // Big Endian Byte Order 
                    using (var br = new BinaryReaderBE(new MemoryStream(action)))
                    {
                        br.BaseStream.Seek(4, SeekOrigin.Begin);

                        var iconNumber = br.ReadUInt16();

                        // Filter out any actions using placeholder icons
                        if (iconNumber == 0 || iconNumber == 405) return;

                        br.BaseStream.Seek(16, SeekOrigin.Begin);

                        var nameLength = action.Length - 16;

                        var name = Encoding.UTF8.GetString(br.ReadBytes(nameLength)).Replace("\0", "");

                        xivUi.Name = name;
                        xivUi.IconNumber = iconNumber;
                    }

                    if (xivUi.Name.Equals(string.Empty)) return;

                    lock (actionLock)
                    {
                        actionNames.Add(xivUi.Name);
                        actionList.Add(xivUi);
                    }
                }));

                // Data from emote_0
                var emoteExData = await _ex.ReadExData(XivEx.emote);

                await Task.Run(() => Parallel.ForEach(emoteExData.Values, (action) =>
                {
                    var xivUi = new XivUi()
                    {
                        PrimaryCategory = "UI",
                        SecondaryCategory = XivStrings.Actions,
                        TertiaryCategory = XivStrings.Emote
                    };

                    // Big Endian Byte Order 
                    using (var br = new BinaryReaderBE(new MemoryStream(action)))
                    {
                        br.BaseStream.Seek(28, SeekOrigin.Begin);

                        var iconNumber = br.ReadUInt16();

                        // Filter out any actions using placeholder icons
                        if (iconNumber == 0 || iconNumber == 405) return;

                        br.BaseStream.Seek(40, SeekOrigin.Begin);

                        var nameLength = action.Length - 40;

                        var name = Encoding.UTF8.GetString(br.ReadBytes(nameLength)).Replace("\0", "");


                        xivUi.Name = name;
                        xivUi.IconNumber = iconNumber;
                    }

                    if (xivUi.Name.Equals(string.Empty)) return;

                    lock (actionLock)
                    {
                        actionNames.Add(xivUi.Name);
                        actionList.Add(xivUi);
                    }
                }));

                // Data from marker_0
                var markerExData = await _ex.ReadExData(XivEx.marker);

                await Task.Run(() => Parallel.ForEach(markerExData.Values, (action) =>
                {
                    var xivUi = new XivUi()
                    {
                        PrimaryCategory = "UI",
                        SecondaryCategory = XivStrings.Actions,
                        TertiaryCategory = XivStrings.Marker
                    };

                    // Big Endian Byte Order 
                    using (var br = new BinaryReaderBE(new MemoryStream(action)))
                    {
                        br.BaseStream.Seek(6, SeekOrigin.Begin);

                        var iconNumber = br.ReadUInt16();

                        // Filter out any actions using placeholder icons
                        if (iconNumber == 0 || iconNumber == 405) return;

                        br.BaseStream.Seek(10, SeekOrigin.Begin);

                        var nameLength = action.Length - 10;

                        var name = Encoding.UTF8.GetString(br.ReadBytes(nameLength)).Replace("\0", "");

                        xivUi.Name = name;
                        xivUi.IconNumber = iconNumber;
                    }

                    if (xivUi.Name.Equals(string.Empty)) return;

                    lock (actionLock)
                    {
                        actionNames.Add(xivUi.Name);
                        actionList.Add(xivUi);
                    }
                }));

                // Data from fieldmarker_0
                var fieldMarkerExData = await _ex.ReadExData(XivEx.fieldmarker);

                await Task.Run(() => Parallel.ForEach(fieldMarkerExData.Values, (action) =>
                {
                    var xivUi = new XivUi()
                    {
                        PrimaryCategory = "UI",
                        SecondaryCategory = XivStrings.Actions,
                        TertiaryCategory = XivStrings.FieldMarker
                    };

                    // Big Endian Byte Order 
                    using (var br = new BinaryReaderBE(new MemoryStream(action)))
                    {
                        br.BaseStream.Seek(8, SeekOrigin.Begin);

                        var iconNumber = br.ReadUInt16();

                        // Filter out any actions using placeholder icons
                        if (iconNumber == 0 || iconNumber == 405) return;

                        br.BaseStream.Seek(12, SeekOrigin.Begin);

                        var nameLength = action.Length - 12;

                        var name = Encoding.UTF8.GetString(br.ReadBytes(nameLength)).Replace("\0", "");

                        xivUi.Name = name;
                        xivUi.IconNumber = iconNumber;
                    }

                    if (xivUi.Name.Equals(string.Empty)) return;
                */

                lock (actionLock)
                {
                    actionNames.Add(xivUi.Name);
                    actionList.Add(xivUi);
                }
            }));

            // Remove any duplicates and return the sorted the list
            actionList = actionList.Distinct().ToList();
            actionList.Sort();

            return actionList;
        }

        /// <summary>
        /// Gets the list of status effect UI elements
        /// </summary>
        /// <returns>A list containing XivUi data</returns>
        public async Task<List<XivUi>> GetStatusList()
        {
            var statusLock = new object();
            var statusList = new List<XivUi>();
            
            var statusExData = await _ex.ReadExData(XivEx.status);

            await Task.Run(() => Parallel.ForEach(statusExData.Values, (status) =>
            {
                var xivUi = new XivUi()
                {
                    PrimaryCategory = "UI",
                    SecondaryCategory = XivStrings.Status,
                    Name = (string) status.GetColumnByName("Name"),
                    IconNumber = (int)((uint) status.GetColumnByName("Icon"))
                };
                if (string.IsNullOrWhiteSpace(xivUi.Name)) return;

                //Status effects have a byte that determines whether the effect is detrimental or beneficial
                var type = (byte)status.GetColumnByName("Type");
                if (type == 1)
                {
                    xivUi.TertiaryCategory = XivStrings.Beneficial;
                }
                else if (type == 2)
                {
                    xivUi.TertiaryCategory = XivStrings.Detrimental;
                }
                else
                {
                    xivUi.TertiaryCategory = XivStrings.None;
                    xivUi.Name = xivUi.Name + " " + type;
                }

            lock (statusLock)
            {
                statusList.Add(xivUi);
            }
            }));

            // Remove any duplicates and return the sorted the list
            statusList = statusList.Distinct().ToList();
            statusList.Sort();

            return statusList;
        }

        /// <summary>
        /// Gets the list of map symbol UI elements
        /// </summary>
        /// <remarks>
        /// The map symbol exd only contains refrences to the placenamedata exd
        /// The names of the symbols are contained withing the placenamedata exd
        /// </remarks>
        /// <returns>A list containing XivUi data</returns>
        public async Task<List<XivUi>> GetMapSymbolList()
        {
            var mapSymbolLock = new object();
            var mapSymbolList = new List<XivUi>();

            var mapSymbolExData = await _ex.ReadExData(XivEx.mapsymbol);
            var placeNameData = await _ex.ReadExData(XivEx.placename);

            await Task.Run(() => Parallel.ForEach(mapSymbolExData.Values, (mapSymbol) =>
            {
                var xivUi = new XivUi()
                {
                    PrimaryCategory = "UI",
                    SecondaryCategory = XivStrings.MapSymbol,
                    IconNumber = (int)mapSymbol.GetColumnByName("Icon"),
                    Name = GetPlaceName(placeNameData, (int)mapSymbol.GetColumnByName("PlaceNameId")),
                };

                if (string.IsNullOrWhiteSpace(xivUi.Name))
                {
                    xivUi.Name = "Unknown Map Symbol #" + xivUi.IconNumber.ToString();
                }

                lock (mapSymbolLock)
                {
                    mapSymbolList.Add(xivUi);
                }
            }));

            mapSymbolList.Sort();

            return mapSymbolList;
        }

        /// <summary>
        /// Gets the list of online status UI elements
        /// </summary>
        /// <returns>A list containing XivUi data</returns>
        public async Task<List<XivUi>> GetOnlineStatusList()
        {
            var onlineStatusLock = new object();
            var onlineStatusList = new List<XivUi>();


            var onlineStatusExData = await _ex.ReadExData(XivEx.onlinestatus);

            await Task.Run(() => Parallel.ForEach(onlineStatusExData.Values, (onlineStatus) =>
            {
                var xivUi = new XivUi()
                {
                    PrimaryCategory = "UI",
                    SecondaryCategory = XivStrings.OnlineStatus,
                    IconNumber = (int)(uint)onlineStatus.GetColumnByName("Icon"),
                    Name = (string)onlineStatus.GetColumnByName("Name"),
                };

                lock (onlineStatusLock)
                {
                    onlineStatusList.Add(xivUi);
                }
            }));

            onlineStatusList.Sort();

            return onlineStatusList;
        }

        /// <summary>
        /// Gets the list of Weather UI elements
        /// </summary>
        /// <returns>A list containing XivUi data</returns>
        public async Task<List<XivUi>> GetWeatherList()
        {
            var weatherLock = new object();
            var weatherList = new List<XivUi>();

            var weatherExData = await _ex.ReadExData(XivEx.weather);

            var weatherNames = new List<string>();

            await Task.Run(() => Parallel.ForEach(weatherExData.Values, (weather) =>
            {
                var xivUi = new XivUi()
                {
                    PrimaryCategory = "UI",
                    SecondaryCategory = XivStrings.Weather,
                    IconNumber = (int)weather.GetColumnByName("Icon"),
                    Name = (string)weather.GetColumnByName("Name"),
                };

                lock (weatherLock)
                {
                    weatherList.Add(xivUi);
                }
            }));

            weatherList.Sort();

            return weatherList;
        }

        /// <summary>
        /// Gets the list of available loading screen images
        /// </summary>
        /// <returns>A list containing XivUi data</returns>
        public async Task<List<XivUi>> GetLoadingImageList()
        {
            var loadingImageLock = new object();
            var loadingImageList = new List<XivUi>();

            var loadingImageExData = await _ex.ReadExData(XivEx.loadingimage);

            await Task.Run(() => Parallel.ForEach(loadingImageExData.Values, (loadingImage) =>
            {
                var xivUi = new XivUi()
                {
                    PrimaryCategory = "UI",
                    SecondaryCategory = XivStrings.LoadingScreen,
                    UiPath = "ui/loadingimage",
                    Name = (string)loadingImage.GetColumnByName("Name"),
                };

                lock (loadingImageLock)
                {
                    loadingImageList.Add(xivUi);
                }
            }));

            loadingImageList.Sort();

            return loadingImageList;
        }

    }
}

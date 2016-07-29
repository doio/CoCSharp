﻿using CoCSharp.Csv;
using CoCSharp.Data.Models;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Diagnostics;

namespace CoCSharp.Logic
{
    /// <summary>
    /// Represents an in-game Clash of Clans building.
    /// </summary>
    public class Building : Buildable<BuildingData>
    {
        #region Constants
        internal const int BaseGameID = 500000000;

        private static readonly PropertyChangedEventArgs s_isLockedChanged = new PropertyChangedEventArgs("IsLocked");
        #endregion

        #region Constructors
        // Constructor that FromJsonReader method is going to use.
        internal Building(Village village) : base(village)
        {
            // Space
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="Building"/> class with the specified <see cref="Village"/> containing
        /// the <see cref="Building"/> and <see cref="BuildingData"/> which is associated with it.
        /// </summary>
        /// 
        /// <param name="village"><see cref="Village"/> containing the <see cref="Building"/>.</param>
        /// <param name="data"><see cref="BuildingData"/> which is associated with this <see cref="Building"/>.</param>
        public Building(Village village, BuildingData data) : base(village, data)
        {
            CheckAndSetTownHall();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Building"/> class with the specified <see cref="Village"/> containing
        /// the <see cref="Building"/> and <see cref="BuildingData"/> which is associated with it and user token object.
        /// </summary>
        /// 
        /// <param name="village"><see cref="Village"/> which contains the <see cref="Building"/>.</param>
        /// <param name="data"><see cref="BuildingData"/> which is associated with this <see cref="Building"/>.</param>
        /// <param name="userToken">User token associated with this <see cref="Building"/>.</param>
        public Building(Village village, BuildingData data, object userToken) : base(village, data, userToken)
        {
            CheckAndSetTownHall();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Building"/> class with the specified <see cref="Village"/> containing the <see cref="Building"/>
        /// and <see cref="BuildingData"/> which is associated with it, X coordinate and Y coordinate.
        /// </summary>
        /// 
        /// <param name="village"><see cref="Village"/> which contains the <see cref="Building"/>.</param>
        /// <param name="data"><see cref="BuildingData"/> which is associated with this <see cref="Building"/>.</param>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        public Building(Village village, BuildingData data, int x, int y) : base(village, data, x, y)
        {
            CheckAndSetTownHall();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Building"/> class with the specified <see cref="Village"/> containing the <see cref="Building"/>
        /// and <see cref="BuildingData"/> which is associated with it, X coordinate, Y coordinate and user token object.
        /// </summary>
        /// 
        /// <param name="village"><see cref="Village"/> which contains the <see cref="Building"/>.</param>
        /// <param name="data"><see cref="BuildingData"/> which is associated with this <see cref="Building"/>.</param>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <param name="userToken">User token associated with this <see cref="Building"/>.</param>
        public Building(Village village, BuildingData data, int x, int y, object userToken) : base(village, data, x, y, userToken)
        {
            CheckAndSetTownHall();
        }
        #endregion

        #region Fields & Properties
        // Building is locked. Mainly for Alliance Castle.
        private bool _isLocked;
        /// <summary>
        /// Gets or sets whether the <see cref="Building"/> is locked.
        /// </summary>
        public bool IsLocked
        {
            get
            {
                return _isLocked;
            }
            set
            {
                if (_isLocked == value)
                    return;

                OnPropertyChanged(s_isLockedChanged);
                _isLocked = value;
            }
        }
        #endregion

        #region Methods
        #region Construction
        /// <summary>
        /// Begins the construction of the <see cref="Building"/> and increases its level by 1
        /// when done if <see cref="Buildable{TCsvData}.IsConstructing"/> is <c>false</c>; otherwise it throws an <see cref="InvalidOperationException"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="Buildable{BuildingData}.IsConstructing"/> is <c>true</c>.</exception>
        public override void BeginConstruction()
        {
            // Make sure that we not constructing a building that is already in construction.
            if (IsConstructing)
                throw new InvalidOperationException("Building object is already in construction.");
            if (!CanUpgrade)
                throw new InvalidOperationException("Building object is maxed or TownHall level too low.");

            Debug.Assert(Data != null && _nextUpgrade != null);

            var buildData = _isConstructed ? _nextUpgrade : Data;
            var startTime = DateTime.UtcNow;

            // No need to schedule construction logic if its construction is instant. (Walls)
            if (buildData.BuildTime == InstantConstructionTime)
            {
                DoConstructionFinished();
                return;
            }

            var constructionTime = startTime.Add(buildData.BuildTime);
            ConstructionEndTime = constructionTime;

            ScheduleBuild();
        }

        /// <summary>
        /// Cancel the construction of the <see cref="Building"/> if <see cref="Buildable{TCsvData}.IsConstructing"/> is <c>true</c>; otherwise
        /// it throws an <see cref="InvalidOperationException"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="Buildable{TCsvData}.IsConstructing"/> is <c>false</c>.</exception>
        public override void CancelConstruction()
        {
            // Make sure that we not canceling construction of a building that is not in construction.
            if (!IsConstructing)
                throw new InvalidOperationException("Building object is not in construction.");

            var endTime = DateTime.UtcNow;

            // Remove the schedule because we don't want it to trigger the events anymore.
            CancelScheduleBuild();

            ConstructionTEndUnixTimestamp = 0;
            OnConstructionFinished(new ConstructionFinishedEventArgs<BuildingData>()
            {
                BuildableConstructed = this,
                EndTime = endTime,
                UserToken = UserToken,
                WasCancelled = true
            });
        }

        /// <summary>
        /// Speeds up the construction of the <see cref="Building"/> and increases its level by 1
        /// instantly if <see cref="Buildable{TCsvData}.IsConstructing"/> is <c>true</c>; otherwise it throws an
        /// <see cref="InvalidOperationException"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="Buildable{TCsvData}.IsConstructing"/> is <c>false</c>.</exception>
        public override void SpeedUpConstruction()
        {
            // Make sure that we not speeding up construction of a building that is not in construction.
            if (!IsConstructing)
                throw new InvalidOperationException("Building object is not in construction.");

            // Remove the schedule because we don't want it to trigger the events anymore.
            CancelScheduleBuild();
            DoConstructionFinished();
        }

        internal override void DoConstructionFinished()
        {
            var endTime = DateTime.UtcNow;

            if (!_isConstructed)
            {
                // If the building is not constructed (level -1) yet we don't update the Data.
                _isConstructed = true;
            }
            else
            {
                // Increase level if construction finished successfully.
                var dataId = Data.ID;
                var lvl = Data.Level + 1;

                //UpdateData(dataId, lvl);
                _data = _nextUpgrade;
                UpdateCanUpgade();
            }

            ConstructionTEndUnixTimestamp = 0;
            OnConstructionFinished(new ConstructionFinishedEventArgs<BuildingData>()
            {
                BuildableConstructed = this,
                UserToken = UserToken,
                EndTime = endTime
            });

            _scheduled = false;
        }

        internal override bool CanUpgradeCheckTownHallLevel()
        {
            Debug.Assert(_nextUpgrade != null && Data != null);


            if (_nextUpgrade.TID == "TID_BUILDING_TOWN_HALL")
                return true;

            var buildData = _isConstructed ? _nextUpgrade : Data;

            var th = Village.TownHall;
            if (th == null)
                throw new InvalidOperationException("Village does not contain a TownHall.");

            // TownHallLevel field is not a zero-based so we subtract 1.
            if (th.Data.Level >= buildData.TownHallLevel - 1)
                return true;

            return false;
        }
        #endregion

        internal override void ResetVillageObject()
        {
            base.ResetVillageObject();
            _isLocked = default(bool);
        }

        internal override void RegisterVillageObject()
        {
            ID = BaseGameID + Village.Buildings.Count;
            Village.Buildings.Add(this);
        }

        #region Json Reading/Writing
        internal override void ToJsonWriter(JsonWriter writer)
        {
            writer.WriteStartObject();

            writer.WritePropertyName("data");
            writer.WriteValue(Data.ID);

            writer.WritePropertyName("id");
            writer.WriteValue(ID);

            writer.WritePropertyName("lvl");
            if (IsConstructing && Data.Level == 0)
            {
                writer.WriteValue(NotConstructedLevel);
            }
            else
            {
                writer.WriteValue(Data.Level);
            }

            if (IsLocked != default(bool))
            {
                writer.WritePropertyName("locked");
                writer.WriteValue(IsLocked);
            }

            if (ConstructionTEndUnixTimestamp != default(int))
            {
                writer.WritePropertyName("const_t_end");
                writer.WriteValue(ConstructionTEndUnixTimestamp);
            }

            if (ConstructionTSeconds != default(int))
            {
                writer.WritePropertyName("const_t");
                writer.WriteValue(ConstructionTSeconds);
            }

            writer.WritePropertyName("x");
            writer.WriteValue(X);

            writer.WritePropertyName("y");
            writer.WriteValue(Y);

            writer.WriteEndObject();
        }

        internal override void FromJsonReader(JsonReader reader)
        {
            var instance = CsvData.GetInstance<BuildingData>();
            // const_t_end value.
            var constTimeEnd = -1;
            // const_t value.
            var constTime = -1;

            var dataId = -1;
            var dataIdSet = false;

            var lvl = -1;
            var lvlSet = false;

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                    break;

                if (reader.TokenType == JsonToken.PropertyName)
                {
                    var propertyName = (string)reader.Value;
                    switch (propertyName)
                    {
                        case "id":
                            // Ignore it for now.
                            break;

                        case "data":
                            dataId = reader.ReadAsInt32().Value;
                            dataIdSet = true;
                            break;

                        case "lvl":
                            lvl = reader.ReadAsInt32().Value;
                            lvlSet = true;
                            break;

                        case "locked":
                            IsLocked = reader.ReadAsBoolean().Value;
                            break;

                        case "const_t_end":
                            constTimeEnd = reader.ReadAsInt32().Value;
                            break;

                        case "const_t":
                            constTime = reader.ReadAsInt32().Value;
                            break;

                        case "x":
                            X = reader.ReadAsInt32().Value;
                            break;

                        case "y":
                            Y = reader.ReadAsInt32().Value;
                            break;
                    }
                }
            }

            if (!dataIdSet)
                throw new InvalidOperationException("Building JSON does not contain a 'data' field.");
            if (!lvlSet)
                throw new InvalidOperationException("Building JSON does not contain a 'lvl' field.");

            // If its not constructed yet, the level is -1,
            // therefore it must be a lvl 0 building.
            if (lvl == NotConstructedLevel)
            {
                _isConstructed = false;
                lvl = 0;
            }
            else
            {
                _isConstructed = true;
            }

            if (instance.InvalidDataID(dataId))
                throw new InvalidOperationException("Building JSON contained an invalid BuildingData ID. " + instance.GetArgsOutOfRangeMessage("Data ID"));

            UpdateData(dataId, lvl);
            // Check if the current building is a townhall.
            // If yes set Village.TownHall to this building.
            CheckAndSetTownHall();

            // Try to use const_t if we were not able to get const_t_end's value.
            if (constTimeEnd == -1)
            {
                // We don't have const_t either so we can exit early.
                if (constTime == -1)
                    return;

                ConstructionEndTime = DateTime.UtcNow.AddSeconds(constTime);
            }
            else
            {
                if (constTimeEnd > DateTimeConverter.UnixUtcNow + 100)
                {
                    ConstructionTEndUnixTimestamp = constTimeEnd;
                }
                else
                {
                    // Date at which building construction was going to end has passed.
                    DoConstructionFinished();
                    return;
                }
            }

            ScheduleBuild();
        }
        #endregion

        // Determines if the current VillageObject is a TownHall building based on Data.TID
        // and set the townhall of the Village to this VillageObject. 
        internal void CheckAndSetTownHall()
        {
            if (Data.TID == "TID_BUILDING_TOWN_HALL")
            {
                // A Village cannot contain more than 1 townhall.
                if (Village.TownHall != null)
                    throw new InvalidOperationException("Village already contains a TownHall.");

                Village.TownHall = this;
            }
        }

        internal static Building GetInstance(Village village)
        {
            var obj = (VillageObject)null;
            if (VillageObjectPool.TryPop(BaseGameID, out obj))
            {
                obj.Village = village;
                return (Building)obj;
            }

            return new Building(village);
        }
        #endregion
    }
}

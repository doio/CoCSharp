﻿using System;
using Newtonsoft.Json;

namespace CoCSharp.Logic.Components
{
    /// <summary>
    /// 
    /// </summary>
    public class UnitStorageComponent : LogicComponent
    {
        internal const int ID = 1;

        internal UnitStorageComponent()
        {
            // Space
        }

        internal override string ComponentName
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        internal override int ComponentID
        {
            get
            {
                return ID;
            }
        }

        internal override void Execute()
        {
            throw new NotImplementedException();
        }

        internal override void FromJsonReader(JsonReader reader)
        {
            throw new NotImplementedException();
        }

        internal override void ToJsonWriter(JsonWriter writer)
        {
            throw new NotImplementedException();
        }
    }
}

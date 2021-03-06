﻿using System;
using System.Collections.Generic;
using System.Text;

namespace database_server
{
    class Record
    {
        public String Key { get; private set; }
        public String Value { get; private set; }
        public Boolean isDead { get; private set; }
        public Record(String Key,String Value,Boolean isDead=false)
        {
            this.Key = Key;
            this.Value = Value;
            this.isDead = isDead;
        }
        public String getRecordRepresentastion()
        {

            return (Key + "\t" + Value + "\t" + isDead.ToString() + Environment.NewLine);
        }
        public static Record getRecordFromString(String representation)
        {
            var parts = representation.Split("\t");
            string key = parts[0];
            string value = parts[1];
            bool isDead;
            bool isSuccess = Boolean.TryParse(parts[2],out isDead);
            if (isSuccess)
            {
                return new Record(key, value, isDead);
            } 
            else
            {
                throw new ArgumentException("Could not parse argument");
            }
        }
    }
}

﻿using System.IO;
using System.Xml.Serialization;

namespace EnhancedHearseAI
{
    public class ModConfiguration
    {
        public bool PrioritizeTargetWithRedSigns;
        public bool MinimizeHearses;

        public ModConfiguration()
		{
            this.PrioritizeTargetWithRedSigns = false;
            this.MinimizeHearses = false;
        }

		public static bool Serialize(string filename, ModConfiguration config)
		{
			XmlSerializer xmlSerializer = new XmlSerializer(typeof(ModConfiguration));
			try
			{
				using (StreamWriter streamWriter = new StreamWriter(filename))
				{
					xmlSerializer.Serialize(streamWriter, config);
					return true;
				}
			}
			catch
			{
			}
			return false;
		}

		public static ModConfiguration Deserialize(string filename)
		{
			XmlSerializer xmlSerializer = new XmlSerializer(typeof(ModConfiguration));
			try
			{
				using (StreamReader streamReader = new StreamReader(filename))
				{
					ModConfiguration modConfiguration = (ModConfiguration)xmlSerializer.Deserialize(streamReader);
					return modConfiguration;
				}
			}
			catch
			{
			}
			return null;
		}
	}
}

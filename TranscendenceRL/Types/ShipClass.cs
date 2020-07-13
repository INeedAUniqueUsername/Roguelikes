﻿using Common;
using System;
using System.Collections.Generic;
using System.Xml.Linq;
namespace TranscendenceRL {
    public class ShipClass : DesignType {
		public string codename;
		public string name;
		public double thrust;
		public double maxSpeed;
		public double rotationMaxSpeed;
		public double rotationDecel;
		public double rotationAccel;
		public StaticTile tile;
		public DamageSystemDesc damageDesc;
		public DeviceList devices;
		public PlayerSettings playerSettings;
		

		public void Initialize(TypeCollection collection, XElement e) {
			codename = e.ExpectAttribute("codename");
			name = e.ExpectAttribute("name");
			thrust = e.ExpectAttributeDouble("thrust");
			maxSpeed = e.ExpectAttributeDouble("maxSpeed");
			rotationMaxSpeed = e.ExpectAttributeDouble("rotationMaxSpeed");
			rotationDecel = e.ExpectAttributeDouble("rotationDecel");
			rotationAccel = e.ExpectAttributeDouble("rotationAccel");

			tile = new StaticTile(e);

			if(e.HasElement("HPSystem", out XElement xmlHPSystem)) {
				damageDesc = new HPSystemDesc(xmlHPSystem);
			} else if(e.HasElement("LayeredArmorSystem", out XElement xmlLayeredArmor)) {
				damageDesc = new LayeredArmorDesc(xmlLayeredArmor);
			} else {
				throw new Exception("<ShipClass> requires either <HPSystem> or <LayeredArmorSystem> subelement");
			}

			if(e.HasElement("Devices", out XElement xmlDevices)) {
				devices = new DeviceList(xmlDevices);
			} else {
				devices = new DeviceList();
			}
			if(e.HasElement("PlayerSettings", out XElement xmlPlayerSettings)) {
				playerSettings = new PlayerSettings(xmlPlayerSettings);
			}
		}
	}
	public interface DamageSystemDesc {
		DamageSystem Create(SpaceObject owner);
	}
	public class HPSystemDesc : DamageSystemDesc {
		public int maxHP;
		public HPSystemDesc(XElement e) {
			maxHP = e.ExpectAttributeInt("maxHP");
		}
		public DamageSystem Create(SpaceObject owner) {
			return new HPSystem(maxHP);
		}
	}
	public class LayeredArmorDesc : DamageSystemDesc {
		ArmorList armorList;
		public LayeredArmorDesc(XElement e) {
			armorList = new ArmorList(e);
		}
		public DamageSystem Create(SpaceObject owner) {
			return new LayeredArmorSystem(armorList.Generate(owner.World.types));
		}
	}
	public class PlayerSettings {
		public bool startingClass;
		public string description;
		public string[] map;
		public PlayerSettings(XElement e) {
			startingClass = e.ExpectAttributeBool("startingClass");
			description = e.ExpectAttribute("description");
			if(e.HasElement("Map", out var xmlMap)) {
				map = xmlMap.Value.Replace("\r", "").Split('\n');
			}
		}
	}
}

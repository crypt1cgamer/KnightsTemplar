using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Carbon;
using Newtonsoft.Json;
using System.IO;

namespace Carbon.Plugins 
{
    public class KnightsTemplar : CarbonPlugin
    {
        private string configPath = Path.Combine(ConVar.Server.rootFolder, "config", "KnightsTemplar.json");
        private static readonly System.Random rng = new System.Random();
        private bool eventActive = false;
        private ConfigData config;
        private List<BasePlayer> activeNPCs = new List<BasePlayer>();

        private class ConfigData
        {
            public int MinNPCs = 4;
            public int MaxNPCs = 10;
            public float EventInterval = 3600f;
            public float EventDuration = 1800f;
            public float AggroRange = 20f;
            public float Accuracy = 0.7f;
            public string NPCPrefab = "assets/prefabs/npc/murderer/murderer.prefab";
            public List<string> AllowedWeapons = new List<string> { "minicrossbow", "bow.compound", "crossbow", "mace" };
            public Dictionary<string, int> LootTable = new Dictionary<string, int>
            {
                { "metal.fragments", 100 },
                { "scrap", 50 },
                { "rifle.body", 1 }
            };
            public Dictionary<string, string> Armor = new Dictionary<string, string>
            {
                { "head", "metal.facemask" },
                { "chest", "metal.plate.torso" },
                { "legs", "roadsign.kilt" },
                { "feet", "boots.frog" }
            };
        }

        public void Init()
        {
            Puts("Knights Templar plugin loaded!");
            LoadConfig();
            StartEventTimer();
        }

        private new void LoadConfig()
        {
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                config = JsonConvert.DeserializeObject<ConfigData>(json) ?? new ConfigData();
            }
            else
            {
                config = new ConfigData();
                SaveConfig();
            }
        }

        private new void SaveConfig()
        {
            File.WriteAllText(configPath, JsonConvert.SerializeObject(config, Formatting.Indented));
        }

        private void StartEventTimer()
        {
            InvokeHandler.Instance.InvokeRepeating(StartKnightsTemplarEvent, config.EventInterval, config.EventInterval);
        }

        private void StartKnightsTemplarEvent()
        {
            if (eventActive) return;
            eventActive = true;

            int npcCount = UnityEngine.Random.Range(config.MinNPCs, config.MaxNPCs + 1);
            for (int i = 0; i < npcCount; i++)
            {
                Vector3 spawnPoint = GetRoadSpawnPosition();
                if (spawnPoint == Vector3.zero) continue;

                var npc = SpawnTemplar(spawnPoint);
                if (npc != null)
                    activeNPCs.Add(npc);
            }

            InvokeHandler.Instance.Invoke(EndEvent, config.EventDuration);
        }

        private Vector3 GetRoadSpawnPosition()
        {
            List<Vector3> roadPositions = GetRoadPositions();
            if (roadPositions.Count == 0)
            {
                Debug.LogWarning("[KnightsTemplar] No valid road positions found!");
                return Vector3.zero;
            }
            return roadPositions[UnityEngine.Random.Range(0, roadPositions.Count)];
        }

        private List<Vector3> GetRoadPositions()
        {
            List<Vector3> roadPositions = new List<Vector3>();
            if (TerrainMeta.Path == null)
            {
                Debug.LogWarning("[KnightsTemplar] No terrain pathing system found!");
                return roadPositions;
            }

            return roadPositions;
        }

        private BasePlayer SpawnTemplar(Vector3 position)
        {
            var npc = GameManager.server.CreateEntity(config.NPCPrefab, position) as BasePlayer;
            if (npc == null)
            {
                Puts("Failed to spawn NPC at " + position);
                return null;
            }

            npc.Spawn();
            EquipNPC(npc);
            AssignBehavior(npc);
            return npc;
        }

        private void EquipNPC(BasePlayer npc)
        {
            if (npc == null) return;

            npc.inventory.containerWear.itemList.Clear();
            npc.inventory.containerMain.itemList.Clear();

            foreach (var item in config.Armor.Values)
            {
                npc.inventory.GiveItem(ItemManager.CreateByName(item));
            }

            string weapon = config.AllowedWeapons[UnityEngine.Random.Range(0, config.AllowedWeapons.Count)];
            npc.inventory.GiveItem(ItemManager.CreateByName(weapon));
        }

        private void AssignBehavior(BasePlayer npc)
        {
            if (npc == null) return;

            BasePlayer target = BasePlayer.activePlayerList
                .Where(p => p != null && p.IsAlive() && !p.IsSleeping() && Vector3.Distance(npc.transform.position, p.transform.position) <= 20f && !p.IsBuildingBlocked())
                .OrderBy(p => Vector3.Distance(npc.transform.position, p.transform.position))
                .FirstOrDefault();

            if (target != null)
            {
                npc.SendMessage("AttackTarget", target, SendMessageOptions.DontRequireReceiver);
            }
        }

        private void EndEvent()
        {
            foreach (var npc in activeNPCs)
            {
                if (npc != null && !npc.IsDestroyed)
                    npc.Kill();
            }

            activeNPCs.Clear();
            eventActive = false;
        }

        public new void Dispose() 
        {
            Puts("Knights Templar plugin unloaded.");
            EndEvent();
        }
    }
}

using HarmonyLib;
using HarmonyLib.Tools;
using Photon.Pun;
using System;
using static AtOEndless.Plugin;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text;
using UnityEngine.UIElements.Collections;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.IO;
using System.Reflection.Emit;
using TMPro;
using System.Drawing;


/*
    AtOManager.Instance.SwapCharacter

*/

namespace AtOEndless
{
    [Serializable]
    public class AtOEndlessSaveData
    {
        public HashSet<string> activeBlessings = [];

        public void FillData()
        {
            LogInfo($"SET ACTIVE BLESSINGS: {string.Join(", ", AtOEndless.activeBlessings)}");
            activeBlessings = AtOEndless.activeBlessings;
        }

        public void LoadData()
        {
            AtOEndless.activeBlessings = activeBlessings;
            LogInfo($"GET ACTIVE BLESSINGS: {string.Join(", ", activeBlessings)}");
        }
    }

    public class AtOEndlessSaveManager : MonoBehaviourPunCallbacks
    {
        private new PhotonView photonView;
        public AtOEndlessSaveData endlessData = new AtOEndlessSaveData();

        public void CreateView()
        {
            photonView = PhotonView.Get(this);
        }

        public void SendData()
        {
            LogInfo($"SEND ACTIVE BLESSINGS: {string.Join(", ", endlessData.activeBlessings)}");
            photonView.RPC("SendDataCo", RpcTarget.All, Functions.CompressString(JsonHelper.ToJson<string>(endlessData.activeBlessings.ToArray())));
        }

        [PunRPC]
        public void SendDataCo(string activeBlessings)
        {
            endlessData.activeBlessings = JsonHelper.FromJson<string>(Functions.DecompressString(activeBlessings)).ToHashSet<string>();
            LogInfo($"SENDCO ACTIVE BLESSINGS: {string.Join(", ", endlessData.activeBlessings)}");
            endlessData.LoadData();
        }

        public void FillData(BinaryFormatter binaryFormatter, CryptoStream cryptoStream)
        {
            endlessData.FillData();
            binaryFormatter.Serialize(cryptoStream, endlessData);
            if (GameManager.Instance.IsMultiplayer() && NetworkManager.Instance.IsMaster())
            {
                LogInfo("SEND DATA FILL");
                SendData();
            }
        }

        public void LoadData(CryptoStream cryptoStream)
        {
            BinaryFormatter binaryFormatter = new BinaryFormatter();
            endlessData = (AtOEndlessSaveData)binaryFormatter.Deserialize(cryptoStream);
            endlessData.LoadData();
            if (GameManager.Instance.IsMultiplayer() && NetworkManager.Instance.IsMaster())
            {
                LogInfo("SEND DATA LOAD");
                SendData();
            }
        }
    }

    [HarmonyPatch]
    public class AtOEndless
    {
        public static HashSet<string> activeBlessings = [];
        public static List<string> availableBlessings = [];

        public static Enums.CardType blessingCardType = Enum.GetValues(typeof(Enums.CardType)).Cast<Enums.CardType>().Max() + 1;
        public static Enums.EventActivation blessingCombatStart = Enum.GetValues(typeof(Enums.EventActivation)).Cast<Enums.EventActivation>().Max() + 1;
        public static Enums.EventActivation blessingBeginRound = Enum.GetValues(typeof(Enums.EventActivation)).Cast<Enums.EventActivation>().Max() + 2;

        public static AtOEndlessSaveManager EndlessSaveManager;

        public static CardData GetRandomBlessing(List<string> ignore = null)
        {
            List<string> stringList = [.. availableBlessings];

            foreach (string activeBlessing in activeBlessings)
            {
                CardData cDataBlessing = Globals.Instance.GetCardData(activeBlessing, false);
                cDataBlessing = Functions.GetCardDataFromCardData(cDataBlessing, "");
                if (cDataBlessing != null && stringList.Contains(cDataBlessing.Id))
                {
                    LogInfo($"Active Blessing: {cDataBlessing.Id}");
                    stringList.Remove(cDataBlessing.Id);
                }
            }

            if (ignore != null)
            {
                foreach (string ignoreBlessing in ignore)
                {
                    CardData cDataBlessing = Globals.Instance.GetCardData(ignoreBlessing, false);
                    cDataBlessing = Functions.GetCardDataFromCardData(cDataBlessing, "");
                    if (cDataBlessing != null && stringList.Contains(cDataBlessing.Id))
                    {
                        LogInfo($"Ignore Blessing: {cDataBlessing.Id}");
                        stringList.Remove(cDataBlessing.Id);
                    }
                }
            }

            LogInfo($"Blessing Cards: {stringList.Count}");

            if (stringList.Count > 0)
            {
                int randomCorruptionIndex = UnityEngine.Random.Range(0, stringList.Count);
                string corruptionIdCard = stringList[randomCorruptionIndex];
                LogInfo($"Random Corruption Index: {randomCorruptionIndex} - {corruptionIdCard}");

                CardData cDataCorruption = Globals.Instance.GetCardData(corruptionIdCard, false);

                if (AtOManager.Instance.GetTownTier() == 0)
                {
                    cDataCorruption = Functions.GetCardDataFromCardData(cDataCorruption, "");
                    if (cDataCorruption != null)
                        corruptionIdCard = cDataCorruption.Id;
                }
                if (AtOManager.Instance.GetTownTier() >= 1)
                {
                    cDataCorruption = Functions.GetCardDataFromCardData(cDataCorruption, "A");
                    if (cDataCorruption != null)
                        corruptionIdCard = cDataCorruption.Id;
                }
                if (AtOManager.Instance.GetTownTier() >= 2)
                {
                    cDataCorruption = Functions.GetCardDataFromCardData(cDataCorruption, "B");
                    if (cDataCorruption != null)
                        corruptionIdCard = cDataCorruption.Id;
                }
                if (AtOManager.Instance.GetTownTier() >= 3)
                {
                    cDataCorruption = Functions.GetCardDataFromCardData(cDataCorruption, "RARE");
                    if (cDataCorruption != null)
                        corruptionIdCard = cDataCorruption.Id;
                }

                if (cDataCorruption == null)
                    cDataCorruption = Globals.Instance.GetCardData(corruptionIdCard, false);

                LogInfo($"Got Corruption Card: {cDataCorruption.Id}");
                return cDataCorruption;
            }
            return null;
        }

        public static void BeginMatchBlessings()
        {
            LogInfo($"BEGIN MATCH BLESSINGS {string.Join(", ", activeBlessings)}");
            Hero[] teamHero = Traverse.Create(MatchManager.Instance).Field("TeamHero").GetValue<Hero[]>();
            LogInfo($"Got Heroes: {teamHero.Length}");
            NPC[] teamNPC = Traverse.Create(MatchManager.Instance).Field("TeamNPC").GetValue<NPC[]>();
            LogInfo($"Got NPCs: {teamNPC.Length}");

            foreach (string blessing in activeBlessings)
            {
                CardData cardData = MatchManager.Instance.GetCardData(blessing);
                LogInfo($"Got Blessing Data for {cardData.Id}");
            }
        }

        public static void CombatStartBlessings()
        {
            LogInfo($"COMBAT START BLESSINGS {string.Join(", ", activeBlessings)}");
            foreach (string blessing in activeBlessings)
            {
                CardData cardData = MatchManager.Instance.GetCardData(blessing);
                if (cardData.Item != null && cardData.Item.Activation == blessingCombatStart)
                {
                    cardData.EnergyCost = 0;
                    cardData.Vanish = true;
                    cardData.CardClass = Enums.CardClass.Boon;
                    MatchManager.Instance.GenerateNewCard(1, blessing, false, Enums.CardPlace.Vanish);
                    Hero[] teamHero = Traverse.Create(MatchManager.Instance).Field("TeamHero").GetValue<Hero[]>();
                    for (int index = 0; index < 4; ++index)
                    {
                        if (teamHero[index] != null && teamHero[index].Alive)
                        {
                            teamHero[index].DoItem(blessingCombatStart, cardData, cardData.Item.Id, null, 0, "", 0, null);
                            break;
                        }
                    }
                }
            }
        }
        public static void BeginRoundBlessings()
        {
            LogInfo($"BEGIN ROUND BLESSINGS {string.Join(", ", activeBlessings)}");
            foreach (string blessing in activeBlessings)
            {
                CardData cardData = MatchManager.Instance.GetCardData(blessing);
                if (cardData.Item != null && cardData.Item.Activation == blessingBeginRound)
                {
                    Hero[] teamHero = Traverse.Create(MatchManager.Instance).Field("TeamHero").GetValue<Hero[]>();
                    NPC[] teamNPC = Traverse.Create(MatchManager.Instance).Field("TeamNPC").GetValue<NPC[]>();
                    LogInfo($"{blessing} - BEGIN ROUND - {cardData.Item.ItemTarget}");
                    if (cardData.Item.ItemTarget == Enums.ItemTarget.AllHero)
                    {
                        for (int index = 0; index < 4; ++index)
                        {
                            if (teamHero[index] != null && teamHero[index].Alive)
                            {
                                teamHero[index].DoItem(blessingBeginRound, cardData, cardData.Item.Id, null, 0, "", 0, null);
                                break;
                            }
                        }
                    }
                    else if (cardData.Item.ItemTarget == Enums.ItemTarget.RandomHero || cardData.Item.ItemTarget == Enums.ItemTarget.Self)
                    {
                        if (cardData.Item.ItemTarget == Enums.ItemTarget.Self)
                        {
                            for (int index = 0; index < 4; ++index)
                            {
                                if (teamHero[index] != null && teamHero[index].Alive)
                                {
                                    teamHero[index].DoItem(blessingBeginRound, cardData, cardData.Item.Id, null, 0, "", 0, null);
                                }
                            }
                        }
                        else
                        {
                            bool flag4 = false;
                            while (!flag4)
                            {
                                int randomIntRange = MatchManager.Instance.GetRandomIntRange(0, 4);
                                if (teamHero[randomIntRange] != null && teamHero[randomIntRange].Alive)
                                {
                                    teamHero[randomIntRange].DoItem(blessingBeginRound, cardData, cardData.Item.Id, null, 0, "", 0, null);
                                    flag4 = true;
                                }
                            }
                        }
                    }
                    else if (cardData.Item.ItemTarget == Enums.ItemTarget.AllEnemy)
                    {
                        for (int index = 0; index < 4; ++index)
                        {
                            if (teamNPC[index] != null && teamNPC[index].Alive)
                            {
                                teamNPC[index].DoItem(blessingBeginRound, cardData, cardData.Item.Id, null, 0, "", 0, null);
                                break;
                            }
                        }
                    }
                    else if (cardData.Item.ItemTarget == Enums.ItemTarget.RandomEnemy || cardData.Item.ItemTarget == Enums.ItemTarget.SelfEnemy)
                    {
                        if (cardData.Item.ItemTarget == Enums.ItemTarget.SelfEnemy)
                        {
                            for (int index = 0; index < 4; ++index)
                            {
                                if (teamNPC[index] != null && teamNPC[index].Alive)
                                {
                                    teamNPC[index].DoItem(blessingBeginRound, cardData, cardData.Item.Id, null, 0, "", 0, null);
                                }
                            }
                        }
                        else
                        {
                            bool flag5 = false;
                            while (!flag5)
                            {
                                int randomIntRange = MatchManager.Instance.GetRandomIntRange(0, 4);
                                if (teamNPC[randomIntRange] != null && teamNPC[randomIntRange].Alive)
                                {
                                    teamNPC[randomIntRange].DoItem(blessingBeginRound, cardData, cardData.Item.Id, null, 0, "", 0, null);
                                    flag5 = true;
                                }
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Enum), nameof(Enum.GetName))]
        public static void GetName(Type enumType, object value, ref string __result)
        {
            if (enumType == typeof(Enums.CardType) && (Enums.CardType)value == blessingCardType)
            {
                __result = "Blessing";
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ItemCombatIcon), nameof(ItemCombatIcon.DoHover))]
        public static void DoHover(ItemCombatIcon __instance, bool state, ref CardData ___cardData)
        {
            if (!(___cardData == null) && ___cardData.CardType == blessingCardType)
            {
                __instance.spriteBackgroundHover.gameObject.SetActive(true);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), nameof(Character.ActivateItem))]
        public static void ActivateItem(ref Character __instance, Enums.EventActivation theEvent, Character target, int auxInt, string auxString, ref bool ___isHero, ref Item ___itemClass, ref CardData ___cardCasted)
        {
            if (MatchManager.Instance == null)
                return;
            int timesActivated = -1;
            foreach (string blessing in activeBlessings)
            {
                CardData cardData = MatchManager.Instance.GetCardData(blessing);
                if (cardData != null)
                {
                    ItemData itemData = null;
                    if (cardData.Item != null)
                        itemData = cardData.Item;
                    else if (cardData.ItemEnchantment != null)
                        itemData = cardData.ItemEnchantment;

                    if (itemData != null && (!itemData.ActivationOnlyOnHeroes || !___isHero) && (itemData.Activation == theEvent || itemData.Activation == Enums.EventActivation.Damaged && theEvent == Enums.EventActivation.DamagedSecondary))
                    {
                        if (Globals.Instance.ShowDebug)
                            Functions.DebugLogGD("[Character/ActivateItem] Checking if " + blessing + " will activate", "item");
                        LogInfo($"{theEvent} - {cardData.Id} - {__instance.HeroIndex} - {itemData.ItemTarget}");
                        if (___itemClass.DoItem(theEvent, cardData, blessing, __instance, target, auxInt, auxString, 0, ___cardCasted, true))
                        {
                            ++timesActivated;
                            if (Globals.Instance.ShowDebug)
                                Functions.DebugLogGD("[Character/ActivateItem] " + blessing + "-> OK", "item");
                            MatchManager.Instance.DoItem(__instance, theEvent, cardData, blessing, target, auxInt, auxString, timesActivated);
                        }
                        else if (Globals.Instance.ShowDebug)
                            Functions.DebugLogGD(blessing + " -> XXXXXX", "item");
                    }
                }
            }
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(MatchManager), "BeginMatch", MethodType.Enumerator)]
        public static IEnumerable<CodeInstruction> BeginMatch_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codeMatcher = new CodeMatcher(instructions, generator);
            codeMatcher.MatchStartForward(
                    new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(MatchManager), "SetInitiatives"))
                )
                .InsertAndAdvance(
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AtOEndless), "BeginMatchBlessings"))
                );
            return codeMatcher.InstructionEnumeration();
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(MatchManager), "NextTurnContinue", MethodType.Enumerator)]
        public static IEnumerable<CodeInstruction> NextTurnContinue_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codeMatcher = new CodeMatcher(instructions, generator);
            codeMatcher.MatchStartForward(
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(MatchManager), "corruptionItem")),
                    new CodeMatch(OpCodes.Ldnull)
                )
                .InsertAndAdvance(
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AtOEndless), "CombatStartBlessings"))
                )
                .Advance(4)
                .MatchStartForward(
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(MatchManager), "corruptionItem")),
                    new CodeMatch(OpCodes.Ldnull)
                )
                .InsertAndAdvance(
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AtOEndless), "BeginRoundBlessings"))
                );

            return codeMatcher.InstructionEnumeration();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Node), nameof(Node.OnMouseUp))]
        public static void OnMouseUp(ref Node __instance)
        {
            if (!Functions.ClickedThisTransform(__instance.transform) || AlertManager.Instance.IsActive() || GameManager.Instance.IsTutorialActive() || SettingsManager.Instance.IsActive() || DamageMeterManager.Instance.IsActive() || (bool)(UnityEngine.Object)MapManager.Instance && MapManager.Instance.IsCharacterUnlock() || (bool)(UnityEngine.Object)MapManager.Instance && (MapManager.Instance.IsCorruptionOver() || MapManager.Instance.IsConflictOver()) || (bool)(UnityEngine.Object)MapManager.Instance && MapManager.Instance.selectedNode || (bool)(UnityEngine.Object)EventManager.Instance)
                return;
            if (SteamManager.Instance.steamId.ToString() == "76561197965495526")
            {
                GameManager.Instance.SetCursorPlain();
                MapManager.Instance.HidePopup();
                MapManager.Instance.PlayerSelectedNode(__instance);
                GameManager.Instance.PlayAudio(AudioManager.Instance.soundButtonClick);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Functions), nameof(Functions.DebugLogGD))]
        public static bool DebugLogGD(string str, string type)
        {
            StringBuilder stringBuilder = new StringBuilder();
            if (type != "")
            {
                stringBuilder.Append("[");
                stringBuilder.Append(type.ToUpper());
                stringBuilder.Append("] ");
            }
            stringBuilder.Append(str);
            LogInfo(stringBuilder.ToString());
            return false;
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(AtOManager), nameof(AtOManager.BeginAdventure))]
        public static void BeginAdventure(ref AtOManager __instance)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            activeBlessings = [];
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MatchManager), "FinishLoadTurnData")]
        public static void FinishLoadTurnData(ref MatchManager __instance)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;

            List<ItemCombatIcon> iconBlessings = [];
            int i = 0;
            foreach (string blessingCardData in activeBlessings)
            {
                CardData blessingCard = Globals.Instance.GetCardData(blessingCardData, false);
                ItemCombatIcon blessingIcon = UnityEngine.Object.Instantiate(__instance.iconCorruption, __instance.iconCorruption.transform.parent);
                blessingIcon.gameObject.name = "CorruptionIcon";
                float x = blessingIcon.transform.localPosition.x - 3f - (0.7f * (i % 6));
                float y = blessingIcon.transform.localPosition.y - (0.7f * Mathf.FloorToInt(i / 6));
                blessingIcon.transform.localPosition = new Vector3(x, y, blessingIcon.transform.localPosition.z + 3f);
                LogInfo($"GAME OBJECT {blessingIcon.gameObject.name}");
                iconBlessings.Add(blessingIcon);
                i++;

                if (blessingCard != null)
                {
                    blessingIcon.transform.gameObject.SetActive(true);
                    blessingIcon.ShowIconCorruption(blessingCard);
                    LogInfo($"CARD ID {blessingCard.Id}");
                }
                else
                {
                    blessingIcon.transform.gameObject.SetActive(false);
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CardData), nameof(CardData.SetDescriptionNew))]
        public static void SetDescriptionNewPre(ref CardData __instance, bool forceDescription, Character character, bool includeInSearch, out Enums.EventActivation __state)
        {
            __state = Enums.EventActivation.None;
            if (__instance.CardType == blessingCardType && __instance.Item != null)
            {
                if (__instance.Item.Activation == blessingBeginRound)
                {
                    __state = __instance.Item.Activation;
                    __instance.Item.Activation = Enums.EventActivation.BeginRound;
                }
                if (__instance.Item.Activation == blessingCombatStart)
                {
                    __state = __instance.Item.Activation;
                    __instance.Item.Activation = Enums.EventActivation.BeginCombat;
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CardData), nameof(CardData.SetDescriptionNew))]
        public static void SetDescriptionNewPost(ref CardData __instance, bool forceDescription, Character character, bool includeInSearch, Enums.EventActivation __state)
        {
            if (__instance.CardType == blessingCardType && __instance.Item != null)
            {
                if (__state != Enums.EventActivation.None)
                    __instance.Item.Activation = __state;
            }
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SaveGame))]
        public static void SaveGame(int slot, bool backUp)
        {
            string saveGameExtensionBK = Traverse.Create(typeof(SaveManager)).Field("saveGameExtensionBK").GetValue<string>();
            string saveGameExtension = Traverse.Create(typeof(SaveManager)).Field("saveGameExtension").GetValue<string>();
            byte[] key = Traverse.Create(typeof(SaveManager)).Field("key").GetValue<byte[]>();
            byte[] iv = Traverse.Create(typeof(SaveManager)).Field("iv").GetValue<byte[]>();

            StringBuilder stringBuilder1 = new StringBuilder();
            stringBuilder1.Append(Application.persistentDataPath);
            stringBuilder1.Append("/");
            stringBuilder1.Append((ulong)SteamManager.Instance.steamId);
            stringBuilder1.Append("/");
            stringBuilder1.Append(GameManager.Instance.ProfileFolder);
            stringBuilder1.Append("endless_");
            stringBuilder1.Append(slot);
            StringBuilder stringBuilder2 = new StringBuilder();
            stringBuilder2.Append(stringBuilder1.ToString());
            stringBuilder2.Append(saveGameExtensionBK);
            stringBuilder1.Append(saveGameExtension);
            string str = stringBuilder1.ToString();
            string destFileName = stringBuilder2.ToString();
            if (backUp && File.Exists(str))
                File.Copy(str, destFileName, true);
            DESCryptoServiceProvider cryptoServiceProvider = new DESCryptoServiceProvider();
            try
            {
                FileStream fileStream = new FileStream(str, FileMode.Create, FileAccess.Write);
                using (CryptoStream cryptoStream = new CryptoStream(fileStream, cryptoServiceProvider.CreateEncryptor(key, iv), CryptoStreamMode.Write))
                {
                    BinaryFormatter binaryFormatter = new BinaryFormatter();
                    EndlessSaveManager.FillData(binaryFormatter, cryptoStream);
                    cryptoStream.Close();
                }
                fileStream.Close();
            }
            catch (Exception ex)
            {
                LogInfo($"Failed to save AtOEndless Data");
                LogInfo($"Reason: {ex.Message} {ex.StackTrace}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.LoadGame))]
        public static void LoadGame(int slot, bool comingFromReloadCombat)
        {
            string saveGameExtension = Traverse.Create(typeof(SaveManager)).Field("saveGameExtension").GetValue<string>();
            byte[] key = Traverse.Create(typeof(SaveManager)).Field("key").GetValue<byte[]>();
            byte[] iv = Traverse.Create(typeof(SaveManager)).Field("iv").GetValue<byte[]>();

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(Application.persistentDataPath);
            stringBuilder.Append("/");
            stringBuilder.Append((ulong)SteamManager.Instance.steamId);
            stringBuilder.Append("/");
            stringBuilder.Append(GameManager.Instance.ProfileFolder);
            stringBuilder.Append("endless_");
            stringBuilder.Append(slot);
            stringBuilder.Append(saveGameExtension);
            string path = stringBuilder.ToString();
            if (!File.Exists(path))
            {
                LogInfo("ERROR File does not exists");
            }
            else
            {
                FileStream fileStream = new FileStream(path, FileMode.Open);
                if (fileStream.Length == 0L)
                {
                    fileStream.Close();
                }
                else
                {
                    DESCryptoServiceProvider cryptoServiceProvider = new DESCryptoServiceProvider();
                    try
                    {
                        CryptoStream serializationStream = new CryptoStream(fileStream, cryptoServiceProvider.CreateDecryptor(key, iv), CryptoStreamMode.Read);
                        EndlessSaveManager.LoadData(serializationStream);
                        serializationStream.Close();
                    }
                    catch (SerializationException ex)
                    {
                        LogInfo("Failed to deserialize LoadGame. Reason: " + ex.Message);
                    }
                    fileStream.Close();
                }
            }
        }



        //FOR TESTING PURPOSES ONLY, ALLOWS CREATING A GAME WITH 1 PLAYER

        /*
                [HarmonyPrefix]
                [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.CreateRoom))]
                public static void DeleteGame(NetworkManager __instance, ref string roomName, ref string roomPlayers, ref string roomPassword, ref string lfm)
                {
                    roomPlayers = "1";
                }

                [HarmonyPostfix]
                [HarmonyPatch(typeof(LobbyManager), nameof(LobbyManager.SetLobbyPlayersData))]
                public static void SetLobbyPlayersData(LobbyManager __instance)
                {
                    __instance.buttonLaunch.gameObject.SetActive(true);
                }
        */



        [HarmonyPostfix]
        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.DeleteGame))]
        public static void DeleteGame(int slot, bool sendTelemetry)
        {
            string saveGameExtension = Traverse.Create(typeof(SaveManager)).Field("saveGameExtension").GetValue<string>();
            string saveGameExtensionBK = Traverse.Create(typeof(SaveManager)).Field("saveGameExtensionBK").GetValue<string>();

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(Application.persistentDataPath);
            stringBuilder.Append("/");
            stringBuilder.Append((ulong)SteamManager.Instance.steamId);
            stringBuilder.Append("/");
            stringBuilder.Append(GameManager.Instance.ProfileFolder);
            stringBuilder.Append("endless_");
            stringBuilder.Append(slot);
            string path1 = stringBuilder.ToString() + saveGameExtension;
            string path2 = stringBuilder.ToString() + saveGameExtensionBK;
            if (File.Exists(path1))
                File.Delete(path1);
            if (File.Exists(path2))
                File.Delete(path2);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Character), nameof(Character.LevelUp))]
        public static void LevelUp(Character __instance, HeroData ___heroData)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;

            if (!AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_allow_additional_levels")))
                return;

            if (___heroData.HeroSubClass.MaxHp.Length < 9)
            {
                int[] maxHp = new int[9];
                ___heroData.HeroSubClass.MaxHp.CopyTo(maxHp, 4);
                ___heroData.HeroSubClass.MaxHp.CopyTo(maxHp, 0);
                ___heroData.HeroSubClass.MaxHp = maxHp;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SubClassData), nameof(SubClassData.GetTraitLevel))]
        public static void GetTraitLevel(SubClassData __instance, string traitName, ref int __result)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;

            if (!AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_allow_additional_levels")))
                return;

            Hero[] team = AtOManager.Instance.GetTeam();
            Hero hero = team.Where(hero => hero.SubclassName.ToLower() == __instance.SubClassName.ToLower()).First();
            if (hero.Traits.Length < 9)
            {
                string[] traits = new string[9];
                hero.Traits.CopyTo(traits, 0);
                hero.Traits = traits;
            }
            if (hero.Traits[__result] != null && hero.Traits[__result] != "" && hero.Traits[__result] != traitName)
                __result += 4;

            LogInfo($"GetTraitLevel: {__result} - {hero.Traits.Length} - {hero.Traits[__result]}");
            foreach (string trait in hero.Traits)
            {
                LogInfo($"GetTraitLevel: {trait}");
            }
        }

        [HarmonyReversePatch]
        [HarmonyPatch(typeof(CharacterWindowUI), "GetTraitData")]
        public static TraitData GetTraitData(CharacterWindowUI __instance, int level, int index)
        {
            return new TraitData();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CharacterWindowUI), "DrawLevelButtons")]
        public static void DrawLevelButtons(ref CharacterWindowUI __instance, int heroLevel, bool levelUp, ref Hero ___currentHero, ref SubClassData ___currentSCD)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;

            if (!AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_allow_additional_levels")))
                return;

            string _color = Globals.Instance.ClassColor[___currentHero.ClassName];
            int characterTier = PlayerManager.Instance.GetCharacterTier("", "trait", ___currentHero.PerkRank);
            for (int level = 1; level < 5; ++level)
            {
                bool _state1 = false;
                bool _state2 = false;
                int index1 = level * 2;
                int index2 = index1 + 1;
                TraitData traitData2 = GetTraitData(__instance, level, 0);
                if (___currentHero.HaveTrait(traitData2.Id))
                    _state2 = true;
                else if ((level == heroLevel || level + 4 == heroLevel) & levelUp && (___currentHero.Owner == null || ___currentHero.Owner == "" || ___currentHero.Owner == NetworkManager.Instance.GetPlayerNick()))
                    if (!___currentHero.HaveTrait(traitData2.Id))
                        _state1 = true;
                __instance.traitLevel[index1].SetHeroIndex(__instance.heroIndex);
                __instance.traitLevel[index1].SetColor(_color);
                __instance.traitLevel[index1].SetPosition(1);
                __instance.traitLevel[index1].SetEnable(_state2);
                __instance.traitLevel[index1].SetActive(_state1);
                __instance.traitLevel[index1].SetTrait(traitData2, characterTier);
                TraitData traitData3 = GetTraitData(__instance, level, 1);
                bool _state3 = false;
                bool _state4 = false;
                if (___currentHero.HaveTrait(traitData3.Id))
                    _state3 = true;
                else if ((level == heroLevel || level + 4 == heroLevel) & levelUp && (___currentHero.Owner == null || ___currentHero.Owner == "" || ___currentHero.Owner == NetworkManager.Instance.GetPlayerNick()))
                    if (!___currentHero.HaveTrait(traitData3.Id))
                        _state4 = true;
                __instance.traitLevel[index2].SetHeroIndex(__instance.heroIndex);
                __instance.traitLevel[index2].SetColor(_color);
                __instance.traitLevel[index2].SetPosition(2);
                __instance.traitLevel[index2].SetEnable(_state3);
                __instance.traitLevel[index2].SetActive(_state4);
                __instance.traitLevel[index2].SetTrait(traitData3, characterTier);
                StringBuilder stringBuilder2 = new StringBuilder();
                bool flag = false;
                if ((level < heroLevel || (level == heroLevel || level + 4 == heroLevel) & levelUp) && (___currentHero.Owner == null || ___currentHero.Owner == "" || ___currentHero.Owner == NetworkManager.Instance.GetPlayerNick()))
                    flag = true;
                stringBuilder2.Append("<size=+.4>");
                if (flag)
                    stringBuilder2.Append("<color=#FC0>");
                stringBuilder2.Append(Texts.Instance.GetText("levelNumber").Replace("<N>", $"{(level + 1)}&{(level + 5)}"));
                if (flag)
                    stringBuilder2.Append("</color>");
                stringBuilder2.Append("</size>");
                stringBuilder2.Append("\n");
                if (flag)
                    stringBuilder2.Append("<color=#EE5A3C>");
                stringBuilder2.Append(Texts.Instance.GetText("incrementMaxHp").Replace("<N>", ___currentSCD.MaxHp[level].ToString()));
                if (flag)
                    stringBuilder2.Append("</color>");
                __instance.traitLevelText[level].text = stringBuilder2.ToString();
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Node), nameof(Node.AssignNode))]
        public static void AssignNodePre(ref AtOManager __instance, out string[][] __state)
        {
            __state = [[.. AtOManager.Instance.mapVisitedNodes], [.. AtOManager.Instance.mapVisitedNodesAction]];
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            AtOManager.Instance.mapVisitedNodes.Clear();
            AtOManager.Instance.mapVisitedNodesAction.Clear();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Node), nameof(Node.AssignNode))]
        public static void AssignNodePost(ref Node __instance, string[][] __state)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            AtOManager.Instance.mapVisitedNodes.AddRange([.. __state[0]]);
            AtOManager.Instance.mapVisitedNodesAction.AddRange([.. __state[1]]);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(AtOManager), nameof(AtOManager.GetTownTier))]
        public static void GetTownTier(ref AtOManager __instance, ref int ___townTier, ref int __result)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            __result = Math.Min(___townTier, 3);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(AtOManager), nameof(AtOManager.GetActNumberForText))]
        public static void GetActNumberForText(ref AtOManager __instance, ref int ___townTier, ref int __result)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            __result = ___townTier + 1;
        }

        public static bool refreshed = false;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MapManager), "Awake")]
        public static void Awake(ref MapManager __instance)
        {
            if (!refreshed)
            {
                foreach (GameObject mapGO in __instance.mapList)
                {
                    foreach (Transform transform in mapGO.transform)
                    {
                        if (transform.gameObject.name == "Nodes")
                        {
                            foreach (Transform transform2 in transform)
                            {
                                GameObject nodeGO = transform2.gameObject;
                                Node node = nodeGO.GetComponent<Node>();
                                if (node.GetComponent<Node>().nodeData != null)
                                {
                                    node.GetComponent<Node>().nodeData = Globals.Instance.GetNodeData(node.nodeData.NodeId);
                                }
                                else
                                {
                                    LogInfo($"No Node Data for {transform2.gameObject.name}");
                                }
                            }
                        }
                    }
                }
                refreshed = true;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(NetworkManager), "Awake")]
        public static void NetworkManagerAwake(ref NetworkManager __instance)
        {
            EndlessSaveManager = NetworkManager.Instance.gameObject.AddComponent<AtOEndlessSaveManager>();
            EndlessSaveManager.CreateView();
        }

        public static string[] GetEnabledPerks()
        {
            List<string> acquiredPerks = [];
            foreach (List<string> sub in AtOManager.Instance.heroPerks.Values)
            {
                foreach (string perk in sub)
                {
                    if (perk.StartsWith("endless_") && !acquiredPerks.Contains(perk))
                        acquiredPerks.Add(perk);
                }
            }
            return [.. acquiredPerks];
        }

        public static string GetRandomPerkDescription(PerkData perkData)
        {
            StringBuilder stringBuilder = new();
            if (perkData.MaxHealth != 0)
                stringBuilder.Append($"<sprite name=heart><space=.5>Health {perkData.MaxHealth:+#;-#;0}<space=1.5>");
            if (perkData.SpeedQuantity != 0)
                stringBuilder.Append($"<sprite name=speedMini><space=.5>Speed {perkData.SpeedQuantity:+#;-#;0}<space=1.5>");
            if (perkData.AuracurseBonus != null && perkData.AuracurseBonusValue != 0)
                stringBuilder.Append($"<sprite name={perkData.AuracurseBonus.Sprite.name}><space=.5>charges {perkData.AuracurseBonusValue:+#;-#;0}<space=1.5>");
            if (perkData.ResistModifiedValue != 0)
            {
                if (perkData.ResistModified == Enums.DamageType.All)
                    stringBuilder.Append($"<sprite name=ui_resistance><space=.5>All resistances {perkData.ResistModifiedValue:+#;-#;0}%<space=1.5>");
                if (perkData.ResistModified != Enums.DamageType.All)
                {
                    string sprite = perkData.ResistModified.ToString().ToLower();
                    if (sprite == "slashing")
                        sprite = "slash";
                    stringBuilder.Append($"<sprite name=resist_{sprite}><space=.5>resistance {perkData.ResistModifiedValue:+#;-#;0}%<space=1.5>");
                }
            }
            if (perkData.DamageFlatBonusValue != 0)
            {
                if (perkData.DamageFlatBonus == Enums.DamageType.All)
                    stringBuilder.Append($"<sprite name=damage><space=.5>All damage {perkData.DamageFlatBonusValue:+#;-#;0}<space=1.5>");
                if (perkData.DamageFlatBonus != Enums.DamageType.All)
                {
                    string sprite = perkData.DamageFlatBonus.ToString().ToLower();
                    if (sprite == "slashing")
                        sprite = "slash";
                    stringBuilder.Append($"<sprite name={sprite}><space=.5>damage {perkData.DamageFlatBonusValue:+#;-#;0}<space=1.5>");
                }
            }
            stringBuilder.Replace("<c>", "");
            stringBuilder.Replace("</c>", "");
            return $"<space=1>{stringBuilder}";
        }

        public static string GetRandomPerkType(string[] exclude)
        {
            string[] types = [.. (new string[] { "h", "a", "d", "r", "s" }).Where(v => !exclude.Contains(v))];
            return types[UnityEngine.Random.Range(0, types.Length)];
        }

        public static string GetRandomPerkSubtype(string type)
        {
            if (type == "a")
            {
                string[] types = ["bleed", "block", "burn", "chill", "dark", "fury", "insane", "poison", "regeneration", "sharp", "shield", "sight", "spark", "thorns", "vitality", "wet"];
                return types[UnityEngine.Random.Range(0, types.Length)];
            }
            if (type == "d" || type == "r")
            {
                Enums.DamageType[] types = [.. Enum.GetValues(typeof(Enums.DamageType)).Cast<Enums.DamageType>().Where(d => !d.Equals(Enums.DamageType.None))];
                return types[UnityEngine.Random.Range(0, types.Length)].ToString().ToLower();
            }
            return "";
        }

        public static int GetRandomPerkValue(string type, string subtype = "")
        {
            int high = 0;
            int low = 0;
            if (type == "h")
            {
                low = 5;
                high = 15;
            }
            if (type == "a")
            {
                low = 1;
                high = 2;
            }
            if (type == "s")
            {
                low = 1;
                high = 2;
            }
            if (type == "d")
            {
                low = 1;
                high = 3;
            }
            if (type == "r")
            {
                low = 5;
                high = 10;
            }
            return UnityEngine.Random.Range(low, high);
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(EventManager), nameof(EventManager.SetEvent))]
        public static void SetEvent(ref EventManager __instance, EventData _eventData)
        {
            if (_eventData.EventId == "e_endless_perk")
            {
                EventReplyData perkReplyPrefab = Globals.Instance.GetEventData("e_challenge_next").Replys.First<EventReplyData>();
                int deterministicHashCode = AtOManager.Instance.GetGameId().GetDeterministicHashCode();
                UnityEngine.Random.InitState(deterministicHashCode);

                List<EventReplyData> replies = [];
                for (int i = 0; i < 5; i++)
                {
                    EventReplyData perkReplyData = perkReplyPrefab.ShallowCopy();
                    StringBuilder sb = new();
                    sb.Append("endless_");
                    List<string> exclude = [];
                    for (int t = 0; t < 2; t++)
                    {
                        string type = GetRandomPerkType([.. exclude]);
                        exclude.Add(type);
                        string subtype = GetRandomPerkSubtype(type);
                        int value = GetRandomPerkValue(type, subtype);
                        sb.Append($"{type}:");
                        if (subtype != "")
                            sb.Append($"{subtype}#{type}v:");
                        sb.Append($"{value}#");
                    }
                    sb.Length--;
                    sb.Append($"_{Functions.RandomString(6f)}");
                    perkReplyData.SsPerkData = Globals.Instance.GetPerkData(sb.ToString());
                    perkReplyData.SsPerkData1 = null;
                    //LogInfo($"{perkReplyData.SsPerkData.Id}");
                    perkReplyData.ReplyText = GetRandomPerkDescription(perkReplyData.SsPerkData);
                    perkReplyData.SsRewardText = "";
                    //LogInfo($"{perkReplyData.ReplyText}");
                    perkReplyData.SsRequirementUnlock = null;
                    perkReplyData.SsDustReward = 0;
                    perkReplyData.SsExperienceReward = 0;
                    perkReplyData.SsGoldReward = 0;
                    perkReplyData.SsFinishObeliskMap = false;
                    perkReplyData.SsEvent = AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_pick_blessing")) ? Globals.Instance.GetEventData("e_endless_blessing") : Globals.Instance.GetEventData("e_endless_obelisk");
                    replies.Add(perkReplyData);
                }
                Globals.Instance.GetEventData("e_endless_perk").Replys = [.. replies];
            }

            if (_eventData.EventId == "e_endless_blessing")
            {
                EventReplyData blessingReplyPrefab = Globals.Instance.GetEventData("e_challenge_next").Replys.First<EventReplyData>();

                int deterministicHashCode = AtOManager.Instance.GetGameId().GetDeterministicHashCode();
                UnityEngine.Random.InitState(deterministicHashCode);

                List<EventReplyData> replies = [];
                List<string> exclude = [];
                for (int i = 0; i < 3; i++)
                {
                    CardData blessing = GetRandomBlessing(exclude);
                    if (blessing != null)
                    {
                        exclude.Add(blessing.Id);
                        EventReplyData blessingReplyData = blessingReplyPrefab.ShallowCopy();
                        blessingReplyData.SsPerkData = null;
                        blessingReplyData.SsPerkData1 = null;
                        blessingReplyData.SsAddCard1 = blessing;
                        blessingReplyData.ReplyShowCard = blessing;
                        blessingReplyData.ReplyText = blessing.CardName;
                        blessingReplyData.SsRewardText = "";
                        blessingReplyData.SsRequirementUnlock = null;
                        blessingReplyData.SsDustReward = 0;
                        blessingReplyData.SsExperienceReward = 0;
                        blessingReplyData.SsGoldReward = 0;
                        blessingReplyData.SsFinishObeliskMap = false;
                        blessingReplyData.SsEvent = Globals.Instance.GetEventData("e_endless_obelisk");
                        replies.Add(blessingReplyData);
                    }
                }

                EventReplyData skipBlessingReplyData = blessingReplyPrefab.ShallowCopy();
                skipBlessingReplyData.SsPerkData = null;
                skipBlessingReplyData.SsPerkData1 = null;
                skipBlessingReplyData.ReplyText = replies.Count == 0 ? "There are no more blessings to choose from." : "Skip";
                skipBlessingReplyData.SsRewardText = "";
                skipBlessingReplyData.SsRequirementUnlock = null;
                skipBlessingReplyData.SsDustReward = 0;
                skipBlessingReplyData.SsExperienceReward = 0;
                skipBlessingReplyData.SsGoldReward = 0;
                skipBlessingReplyData.SsFinishObeliskMap = false;
                skipBlessingReplyData.SsEvent = Globals.Instance.GetEventData("e_endless_obelisk");
                replies.Add(skipBlessingReplyData);

                Globals.Instance.GetEventData("e_endless_blessing").Replys = [.. replies];
            }

            if (_eventData.EventId == "e_endless_obelisk")
            {
                EventReplyData obeliskReplyPrefab = Globals.Instance.GetEventData("e_sen34_a").Replys.First();
                Enums.Zone zone = AtOManager.Instance.GetMapZone(AtOManager.Instance.currentMapNode);

                int deterministicHashCode = AtOManager.Instance.GetGameId().GetDeterministicHashCode();
                UnityEngine.Random.InitState(deterministicHashCode);

                bool canUlmin = SteamManager.Instance.PlayerHaveDLC("2511580") || (GameManager.Instance.IsMultiplayer() && NetworkManager.Instance.AnyPlayersHaveSku("2511580"));
                bool canSahti = SteamManager.Instance.PlayerHaveDLC("3185630") || (GameManager.Instance.IsMultiplayer() && NetworkManager.Instance.AnyPlayersHaveSku("3185630"));

                List<NodeData> possibleNodes = [];
                possibleNodes.Add(Globals.Instance.GetNodeData("sen_0"));
                possibleNodes.Add(Globals.Instance.GetNodeData("faen_0"));
                possibleNodes.Add(Globals.Instance.GetNodeData("aqua_0"));
                possibleNodes.Add(Globals.Instance.GetNodeData("velka_0"));
                if (canUlmin)
                    possibleNodes.Add(Globals.Instance.GetNodeData("ulmin_0"));
                if (canSahti)
                    possibleNodes.Add(Globals.Instance.GetNodeData("sahti_0"));
                possibleNodes.Add(Globals.Instance.GetNodeData("voidlow_0"));

                bool noUnlock = false;

                if (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_unique_zones")))
                {
                    if ((AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_complete_sen")) || zone == Enums.Zone.Senenthia) &&
                       (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_complete_faen")) || zone == Enums.Zone.Faeborg) &&
                       (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_complete_aqua")) || zone == Enums.Zone.Aquarfall) &&
                       (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_complete_velka")) || zone == Enums.Zone.Velkarath) &&
                       (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_complete_ulmin")) || zone == Enums.Zone.Ulminin) &&
                       (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_complete_sahti")) || zone == Enums.Zone.Sahti) &&
                       (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_complete_void")) || zone == Enums.Zone.VoidHigh))
                    {
                        AtOManager.Instance.RemovePlayerRequirement(Globals.Instance.GetRequirementData("endless_complete_sen"));
                        AtOManager.Instance.RemovePlayerRequirement(Globals.Instance.GetRequirementData("endless_complete_faen"));
                        AtOManager.Instance.RemovePlayerRequirement(Globals.Instance.GetRequirementData("endless_complete_aqua"));
                        AtOManager.Instance.RemovePlayerRequirement(Globals.Instance.GetRequirementData("endless_complete_velka"));
                        AtOManager.Instance.RemovePlayerRequirement(Globals.Instance.GetRequirementData("endless_complete_ulmin"));
                        AtOManager.Instance.RemovePlayerRequirement(Globals.Instance.GetRequirementData("endless_complete_sahti"));
                        AtOManager.Instance.RemovePlayerRequirement(Globals.Instance.GetRequirementData("endless_complete_void"));
                        noUnlock = true;
                    }
                }

                if (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_require_all_before_void")))
                {
                    if (zone == Enums.Zone.VoidHigh)
                    {
                        AtOManager.Instance.RemovePlayerRequirement(Globals.Instance.GetRequirementData("endless_complete_sen"));
                        AtOManager.Instance.RemovePlayerRequirement(Globals.Instance.GetRequirementData("endless_complete_faen"));
                        AtOManager.Instance.RemovePlayerRequirement(Globals.Instance.GetRequirementData("endless_complete_aqua"));
                        AtOManager.Instance.RemovePlayerRequirement(Globals.Instance.GetRequirementData("endless_complete_velka"));
                        AtOManager.Instance.RemovePlayerRequirement(Globals.Instance.GetRequirementData("endless_complete_ulmin"));
                        AtOManager.Instance.RemovePlayerRequirement(Globals.Instance.GetRequirementData("endless_complete_sahti"));
                        AtOManager.Instance.RemovePlayerRequirement(Globals.Instance.GetRequirementData("endless_complete_void"));
                        noUnlock = true;
                    }
                }

                if (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_unique_zones")) ||
                  !AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_allow_repeats")))
                {
                    if (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_complete_sen")) || zone == Enums.Zone.Senenthia)
                        possibleNodes.Remove(Globals.Instance.GetNodeData("sen_0"));
                    if (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_complete_faen")) || zone == Enums.Zone.Faeborg)
                        possibleNodes.Remove(Globals.Instance.GetNodeData("faen_0"));
                    if (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_complete_aqua")) || zone == Enums.Zone.Aquarfall)
                        possibleNodes.Remove(Globals.Instance.GetNodeData("aqua_0"));
                    if (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_complete_velka")) || zone == Enums.Zone.Velkarath)
                        possibleNodes.Remove(Globals.Instance.GetNodeData("velka_0"));
                    if (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_complete_ulmin")) || zone == Enums.Zone.Ulminin)
                        possibleNodes.Remove(Globals.Instance.GetNodeData("ulmin_0"));
                    if (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_complete_sahti")) || zone == Enums.Zone.Sahti)
                        possibleNodes.Remove(Globals.Instance.GetNodeData("sahti_0"));
                    if (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_complete_void")) || zone == Enums.Zone.VoidHigh)
                        possibleNodes.Remove(Globals.Instance.GetNodeData("voidlow_0"));
                }

                if (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_require_all_before_void")))
                {
                    if ((!AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_complete_sen")) && zone != Enums.Zone.Senenthia) ||
                       (!AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_complete_faen")) && zone != Enums.Zone.Faeborg) ||
                       (!AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_complete_aqua")) && zone != Enums.Zone.Aquarfall) ||
                       (!AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_complete_velka")) && zone != Enums.Zone.Velkarath) ||
                       (canUlmin && !AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_complete_ulmin")) && zone != Enums.Zone.Ulminin) ||
                       (canSahti && !AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_complete_sahti")) && zone != Enums.Zone.Sahti))
                    {
                        possibleNodes.Remove(Globals.Instance.GetNodeData("voidlow_0"));
                    }
                }

                List<EventReplyData> replies = [];
                int zoneCount = -1;

                if (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_zonecount_1")))
                {
                    zoneCount = 1;
                }
                if (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_zonecount_2")))
                {
                    zoneCount = 2;
                }
                if (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_zonecount_3")))
                {
                    zoneCount = 3;
                }

                int nodeCount = zoneCount == -1 ? possibleNodes.Count : Math.Min(possibleNodes.Count, zoneCount);
                for (int i = 0; i < nodeCount; i++)
                {
                    NodeData selectedNode = possibleNodes[UnityEngine.Random.Range(0, possibleNodes.Count)];

                    EventReplyData obeliskReplyData = obeliskReplyPrefab.ShallowCopy();

                    obeliskReplyData.ReplyActionText = Enums.EventAction.None;
                    obeliskReplyData.SsRequirementUnlock = null;
                    if (!noUnlock)
                    {
                        if (zone == Enums.Zone.Senenthia)
                        {
                            obeliskReplyData.SsRequirementUnlock = Globals.Instance.GetRequirementData("endless_complete_sen");
                        }
                        else if (zone == Enums.Zone.Faeborg)
                        {
                            obeliskReplyData.SsRequirementUnlock = Globals.Instance.GetRequirementData("endless_complete_faen");
                        }
                        else if (zone == Enums.Zone.Aquarfall)
                        {
                            obeliskReplyData.SsRequirementUnlock = Globals.Instance.GetRequirementData("endless_complete_aqua");
                        }
                        else if (zone == Enums.Zone.Velkarath)
                        {
                            obeliskReplyData.SsRequirementUnlock = Globals.Instance.GetRequirementData("endless_complete_velka");
                        }
                        else if (zone == Enums.Zone.Ulminin)
                        {
                            obeliskReplyData.SsRequirementUnlock = Globals.Instance.GetRequirementData("endless_complete_ulmin");
                        }
                        else if (zone == Enums.Zone.Sahti)
                        {
                            obeliskReplyData.SsRequirementUnlock = Globals.Instance.GetRequirementData("endless_complete_sahti");
                        }
                        else if (zone == Enums.Zone.VoidHigh)
                        {
                            obeliskReplyData.SsRequirementUnlock = Globals.Instance.GetRequirementData("endless_complete_void");
                        }
                    }

                    obeliskReplyData.ReplyText = zoneCount == 1 ? "You are unable to focus and your mind races as you approach the obelisk." : GetPortalString(AtOManager.Instance.GetMapZone(selectedNode.NodeId));
                    obeliskReplyData.SsRewardText = "";

                    obeliskReplyData.SsNodeTravel = selectedNode;
                    replies.Add(obeliskReplyData);

                    possibleNodes.Remove(selectedNode);
                }

                Globals.Instance.GetEventData("e_endless_obelisk").Replys = [.. replies];
            }
        }

        public static Dictionary<Enums.Zone, string> portalStrings = new()
        {
            { Enums.Zone.Senenthia, "a grassy" },
            { Enums.Zone.Faeborg, "an icy" },
            { Enums.Zone.Aquarfall, "a swampy" },
            { Enums.Zone.Velkarath, "a molten" },
            { Enums.Zone.Ulminin, "a sandy" },
            { Enums.Zone.Sahti, "a salty" },
            { Enums.Zone.VoidLow, "a cosmic" }
        };

        public static string GetPortalString(Enums.Zone zone)
        {
            return $"Your focus on {portalStrings.Get(zone)} world.";
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(EventManager), "FinalResolution")]
        public static void FinalResolutionPre(EventManager __instance, EventData ___currentEvent, EventReplyData ___replySelected)
        {
            if (___currentEvent.EventId == "e_endless_blessing")
            {
                if (___replySelected != null && ___replySelected.SsAddCard1 != null)
                {
                    CardData blessingCard = ___replySelected.SsAddCard1;

                    LogInfo($"Selected blessing: {blessingCard.Id}");

                    activeBlessings.Add(blessingCard.Id);

                    if (blessingCard != null)
                    {
                        activeBlessings.Add(blessingCard.Id);
                        if (blessingCard.Item != null && blessingCard.Item.MaxHealth > 0)
                        {
                            Hero[] team = AtOManager.Instance.GetTeam();
                            for (int index = 0; index < 4; ++index)
                            {
                                if (team[index] != null && team[index].HeroData != null)
                                {
                                    team[index].Hp += blessingCard.Item.MaxHealth;
                                    team[index].HpCurrent += blessingCard.Item.MaxHealth;
                                    team[index].SetHP();

                                    team[index].ClearCaches();
                                }
                            }
                        }
                    }

                    ___replySelected.SsAddCard1 = null;
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(EventManager), "FinalResolution")]
        public static void FinalResolutionPost(EventManager __instance, EventData ___currentEvent, EventReplyData ___replySelected)
        {
            if (___currentEvent.EventId == "e_endless_perk")
            {
                if (___replySelected != null)
                {
                    __instance.result.text = GetRandomPerkDescription(___replySelected.SsPerkData);
                }
            }
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(Globals), nameof(Globals.GetPerkData))]
        public static void GetPerkData(Globals __instance, ref PerkData __result, ref Dictionary<string, PerkData> ____PerksSource, string id)
        {
            if (__result == null)
            {
                if (id.StartsWith("endless_"))
                {
                    __result = ScriptableObject.CreateInstance<PerkData>();
                    __result.Icon = Globals.Instance.GetAuraCurseData("burn").Sprite;
                    __result.Id = id.ToLower();
                    string[] idparts = id.Split('_');
                    string data = idparts[1];
                    foreach (string part in data.Split('#'))
                    {
                        string[] parts = part.Split(':');
                        string type = parts[0];
                        string value = parts[1];
                        switch (type)
                        {
                            case "h":
                                __result.MaxHealth = int.Parse(value);
                                break;
                            case "s":
                                __result.SpeedQuantity = int.Parse(value);
                                break;
                            case "a":
                                __result.AuracurseBonus = Globals.Instance.GetAuraCurseData(value);
                                break;
                            case "av":
                                __result.AuracurseBonusValue = int.Parse(value);
                                break;
                            case "d":
                                __result.DamageFlatBonus = (Enums.DamageType)Enum.Parse(typeof(Enums.DamageType), $"{char.ToUpper(value[0])}{value.Substring(1).ToLower()}");
                                break;
                            case "dv":
                                __result.DamageFlatBonusValue = int.Parse(value);
                                break;
                            case "r":
                                __result.ResistModified = (Enums.DamageType)Enum.Parse(typeof(Enums.DamageType), $"{char.ToUpper(value[0])}{value.Substring(1).ToLower()}");
                                break;
                            case "rv":
                                __result.ResistModifiedValue = int.Parse(value);
                                break;
                            case "e":
                                __result.EnergyBegin = int.Parse(value);
                                break;
                            default:
                                break;
                        }
                    }
                    __result.Init();
                    ____PerksSource.Add(__result.Id, __result);
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(AtOManager), nameof(AtOManager.AddPerkToHero))]
        public static void AddPerkToHeroPost(AtOManager __instance, ref Hero[] ___teamAtO, int _heroIndex, string _perkId, bool _initHealth)
        {
            PerkData perkData = Globals.Instance.GetPerkData(_perkId);
            if (!(perkData != null))
                return;
            string subclassName = ___teamAtO[_heroIndex].SubclassName;
        }

        public static void AddNewRequirement(string id, ref Dictionary<string, EventRequirementData> ____Requirements)
        {
            if (____Requirements.TryGetValue("_tier2", out EventRequirementData requirementPrefab))
            {
                EventRequirementData endlessRequirement = UnityEngine.Object.Instantiate(requirementPrefab);
                endlessRequirement.RequirementId = endlessRequirement.name = id;
                ____Requirements.Add(endlessRequirement.RequirementId.ToLower(), endlessRequirement);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CardItem), "SetCard")]
        public static void SetCard(CardItem __instance, string id, bool deckScale, Hero _theHero, NPC _theNPC, bool GetFromGlobal, bool _generated,
            ref CardData ___cardData,
            ref Transform ___targetTextT,
            ref Transform ___targetT,
            ref Transform ___requireTextT,
            ref Transform ___typeText,
            ref Transform ___typeTextImage,
            ref Transform ___descriptionTextT,
            ref TMP_Text ___descriptionTextTM
        )
        {
            if (___cardData.CardType == blessingCardType)
            {

                if (___targetTextT.gameObject.activeSelf)
                    ___targetTextT.gameObject.SetActive(false);
                if (___targetT.gameObject.activeSelf)
                    ___targetT.gameObject.SetActive(false);
                if (___requireTextT.gameObject.activeSelf)
                    ___requireTextT.gameObject.SetActive(false);
                if (___typeText.gameObject.activeSelf)
                    ___typeText.gameObject.SetActive(false);
                if (___typeTextImage.gameObject.activeSelf)
                    ___typeTextImage.gameObject.SetActive(false);
                ___descriptionTextT.localPosition = new Vector3(___descriptionTextT.localPosition.x, -0.63f, ___descriptionTextT.localPosition.z);
                ___descriptionTextTM.margin = new Vector4(0.02f, -0.02f, 0.02f, -0.04f);

            }
        }

        public static CardData AddNewCard(string id, string baseId, ref Dictionary<string, CardData> ____CardsSource, ref Dictionary<string, CardData> ____Cards)
        {
            if (____CardsSource.TryGetValue(baseId, out CardData cardPrefab))
            {
                LogInfo($"Adding new card: {id} from {baseId}");
                CardData newCard = UnityEngine.Object.Instantiate(cardPrefab);
                newCard.Id = id;
                newCard.InternalId = id;

                if (newCard.Item != null)
                {
                    newCard.Item = UnityEngine.Object.Instantiate(newCard.Item);
                    newCard.Item.Id = id;
                }
                if (newCard.ItemEnchantment != null)
                {
                    newCard.ItemEnchantment = UnityEngine.Object.Instantiate(newCard.ItemEnchantment);
                    newCard.ItemEnchantment.Id = id;
                }

                ____CardsSource.Add(newCard.Id.ToLower(), newCard);
                ____Cards.Add(newCard.Id.ToLower(), newCard);
                return newCard;
            }
            return null;
        }

        private static void InitNewCard(CardData newCard,
            ref Dictionary<Enums.CardType, List<string>> ____CardItemByType,
            ref Dictionary<Enums.CardType, List<string>> ____CardListByType,
            ref Dictionary<Enums.CardClass, List<string>> ____CardListByClass,
            ref List<string> ____CardListNotUpgraded,
            ref Dictionary<Enums.CardClass, List<string>> ____CardListNotUpgradedByClass,
            ref Dictionary<string, List<string>> ____CardListByClassType,
            ref Dictionary<string, int> ____CardEnergyCost)
        {
            newCard.InitClone(newCard.Id);

            ____CardEnergyCost.Add(newCard.Id, newCard.EnergyCost);
            Globals.Instance.IncludeInSearch(newCard.CardName, newCard.Id);
            ____CardListByClass[newCard.CardClass].Add(newCard.Id);
            if (newCard.CardUpgraded == Enums.CardUpgraded.No)
            {
                ____CardListNotUpgradedByClass[newCard.CardClass].Add(newCard.Id);
                ____CardListNotUpgraded.Add(newCard.Id);
                if (newCard.CardClass == Enums.CardClass.Item)
                {
                    if (!____CardItemByType.ContainsKey(newCard.CardType))
                        ____CardItemByType.Add(newCard.CardType, new List<string>());
                    ____CardItemByType[newCard.CardType].Add(newCard.Id);
                }
            }
            List<Enums.CardType> cardTypes = newCard.GetCardTypes();
            for (int index = 0; index < cardTypes.Count; ++index)
            {
                if (!____CardListByType.ContainsKey(cardTypes[index]))
                    ____CardListByType.Add(cardTypes[index], new List<string>());
                ____CardListByType[cardTypes[index]].Add(newCard.Id);
                string key2 = Enum.GetName(typeof(Enums.CardClass), newCard.CardClass) + "_" + Enum.GetName(typeof(Enums.CardType), cardTypes[index]);
                if (!____CardListByClassType.ContainsKey(key2))
                    ____CardListByClassType[key2] = new List<string>();
                ____CardListByClassType[key2].Add(newCard.Id);
                Globals.Instance.IncludeInSearch(Texts.Instance.GetText(Enum.GetName(typeof(Enums.CardType), cardTypes[index])), newCard.Id);
            }

            newCard.InitClone2();
            newCard.SetDescriptionNew(true);
        }

        public static CardData CloneBlessingCard(string cardId,
            bool isBlessing,
            ref Dictionary<string, CardData> ____CardsSource,
            ref Dictionary<string, CardData> ____Cards,
            ref Dictionary<Enums.CardType, List<string>> ____CardItemByType,
            ref Dictionary<Enums.CardType, List<string>> ____CardListByType,
            ref Dictionary<Enums.CardClass, List<string>> ____CardListByClass,
            ref List<string> ____CardListNotUpgraded,
            ref Dictionary<Enums.CardClass, List<string>> ____CardListNotUpgradedByClass,
            ref Dictionary<string, List<string>> ____CardListByClassType,
            ref Dictionary<string, int> ____CardEnergyCost)
        {

            if (____Cards.TryGetValue($"endless{cardId}", out CardData oldCard))
            {
                LogInfo($"Got existing card: {oldCard.Id}");
                return oldCard;
            }

            LogInfo($"Creating blessing card endless{cardId}");

            CardData newCard = AddNewCard($"endless{cardId}", cardId, ref ____CardsSource, ref ____Cards);
            newCard.CardName = $"Blessing: {newCard.CardName}";

            if (isBlessing)
                newCard.CardType = blessingCardType;
            newCard.CardClass = Enums.CardClass.Special;

            ItemData newCardItem = newCard.Item ?? newCard.ItemEnchantment;
            if (newCardItem != null)
            {
                if (newCardItem.CastedCardType != Enums.CardType.None)
                    newCardItem.CastedCardType = Enums.CardType.None;

                if (newCardItem.ItemTarget == Enums.ItemTarget.AllEnemy)
                    newCardItem.ItemTarget = Enums.ItemTarget.AllHero;
                else if (newCardItem.ItemTarget == Enums.ItemTarget.AllHero)
                    newCardItem.ItemTarget = Enums.ItemTarget.AllEnemy;

                if (newCardItem.ItemTarget == Enums.ItemTarget.RandomEnemy)
                    newCardItem.ItemTarget = Enums.ItemTarget.RandomHero;
                else if (newCardItem.ItemTarget == Enums.ItemTarget.RandomHero)
                    newCardItem.ItemTarget = Enums.ItemTarget.RandomEnemy;

                if (newCardItem.ItemTarget == Enums.ItemTarget.LowestFlatHpEnemy)
                    newCardItem.ItemTarget = Enums.ItemTarget.LowestFlatHpHero;
                else if (newCardItem.ItemTarget == Enums.ItemTarget.LowestFlatHpHero)
                    newCardItem.ItemTarget = Enums.ItemTarget.LowestFlatHpEnemy;

                if (newCardItem.ItemTarget == Enums.ItemTarget.HighestFlatHpEnemy)
                    newCardItem.ItemTarget = Enums.ItemTarget.HighestFlatHpHero;
                else if (newCardItem.ItemTarget == Enums.ItemTarget.HighestFlatHpHero)
                    newCardItem.ItemTarget = Enums.ItemTarget.HighestFlatHpEnemy;

                if (newCardItem.ItemTarget == Enums.ItemTarget.Self)
                    newCardItem.ItemTarget = Enums.ItemTarget.SelfEnemy;
                else if (newCardItem.ItemTarget == Enums.ItemTarget.SelfEnemy)
                    newCardItem.ItemTarget = Enums.ItemTarget.Self;

                if (newCardItem.CardPlace == Enums.CardPlace.TopDeck && newCardItem.CardNum > 0)
                    newCardItem.CardPlace = Enums.CardPlace.Hand;

                if (newCardItem.CardPlace == Enums.CardPlace.Hand && newCardItem.CardNum > 0 && (newCardItem.Activation == Enums.EventActivation.BeginRound || newCardItem.Activation == Enums.EventActivation.CorruptionBeginRound))
                    newCardItem.Activation = Enums.EventActivation.BeginTurnCardsDealt;

                if (newCardItem.CardNum == 0 && newCardItem.Activation == Enums.EventActivation.CorruptionBeginRound)
                    newCardItem.Activation = blessingBeginRound;

                if (newCardItem.CardNum == 0 && newCardItem.Activation == Enums.EventActivation.CorruptionCombatStart)
                    newCardItem.Activation = blessingCombatStart;

                if (newCardItem.CardToGain != null)
                {
                    newCardItem.CardToGain = CloneBlessingCard(newCardItem.CardToGain.Id, false, ref ____CardsSource, ref ____Cards, ref ____CardItemByType, ref ____CardListByType, ref ____CardListByClass, ref ____CardListNotUpgraded, ref ____CardListNotUpgradedByClass, ref ____CardListByClassType, ref ____CardEnergyCost);
                    newCardItem.CardToGain.Playable = true;
                    if (newCard.RelatedCard != "")
                        newCard.RelatedCard = newCardItem.CardToGain.Id;
                }
                else if (newCardItem.CardToGainList != null && newCardItem.CardToGainList.Count > 0)
                {
                    List<CardData> newCardsToGain = [];
                    foreach (CardData card in newCardItem.CardToGainList)
                    {
                        CardData newCardToGain = CloneBlessingCard(card.Id, false, ref ____CardsSource, ref ____Cards, ref ____CardItemByType, ref ____CardListByType, ref ____CardListByClass, ref ____CardListNotUpgraded, ref ____CardListNotUpgradedByClass, ref ____CardListByClassType, ref ____CardEnergyCost);
                        newCardToGain.Playable = true;
                        newCardsToGain.Add(newCardToGain);
                    }
                    newCardItem.CardToGainList = newCardsToGain;
                    if (newCardItem.CardToGainList.Count > 0 && newCard.RelatedCard != "")
                        newCard.RelatedCard = newCardItem.CardToGainList[0].Id;
                    if (newCardItem.CardToGainList.Count > 1 && newCard.RelatedCard2 != "")
                        newCard.RelatedCard2 = newCardItem.CardToGainList[1].Id;
                    if (newCardItem.CardToGainList.Count > 2 && newCard.RelatedCard3 != "")
                        newCard.RelatedCard3 = newCardItem.CardToGainList[2].Id;
                }

                if (newCard.ItemEnchantment != null)
                    newCard.ItemEnchantment = newCardItem;
                else
                    newCard.Item = newCardItem;
            }

            Traverse.Create(newCard).Field("descriptionId").SetValue("");
            Traverse.Create(newCard).Field("effectRequired").SetValue("");

            if (newCard.UpgradedFrom != "")
            {
                newCard.UpgradedFrom = $"endless{newCard.UpgradedFrom.ToLower()}";
            }

            if (newCard.UpgradesTo1 != "")
            {
                CardData upgradesTo1Card = CloneBlessingCard(newCard.UpgradesTo1.ToLower(), true, ref ____CardsSource, ref ____Cards, ref ____CardItemByType, ref ____CardListByType, ref ____CardListByClass, ref ____CardListNotUpgraded, ref ____CardListNotUpgradedByClass, ref ____CardListByClassType, ref ____CardEnergyCost);
                newCard.UpgradesTo1 = upgradesTo1Card.Id;
            }

            if (newCard.UpgradesTo2 != "")
            {
                CardData upgradesTo2Card = CloneBlessingCard(newCard.UpgradesTo2.ToLower(), true, ref ____CardsSource, ref ____Cards, ref ____CardItemByType, ref ____CardListByType, ref ____CardListByClass, ref ____CardListNotUpgraded, ref ____CardListNotUpgradedByClass, ref ____CardListByClassType, ref ____CardEnergyCost);
                newCard.UpgradesTo2 = upgradesTo2Card.Id;
            }

            if (newCard.UpgradesToRare != null)
            {
                CardData upgradesToRareCard = CloneBlessingCard(newCard.UpgradesToRare.Id, true, ref ____CardsSource, ref ____Cards, ref ____CardItemByType, ref ____CardListByType, ref ____CardListByClass, ref ____CardListNotUpgraded, ref ____CardListNotUpgradedByClass, ref ____CardListByClassType, ref ____CardEnergyCost);
                newCard.UpgradesToRare = upgradesToRareCard;
            }

            InitNewCard(newCard, ref ____CardItemByType, ref ____CardListByType, ref ____CardListByClass, ref ____CardListNotUpgraded, ref ____CardListNotUpgradedByClass, ref ____CardListByClassType, ref ____CardEnergyCost);

            return newCard;
        }

        public static CardData CreateCard()
        {
            return null;
        }

        public static List<string> blessingCards = [
            "armageddon", // YES
            //"ashysky", // SHUFFLE INTO DECK
            //"backlash", // NEED DESCRIPTION
            //"bloodpuddle", // YES
            //"bomblottery",
            //"burningweapons", // MAKE IT WORK
            //"chaospuddle", // YES
            "chaoticwind", // YES
            //"christmastree", // NEED DESCRIPTION
            "coldfront", // YES
            //"colorfulpuddle", // YES
            //"darkpuddle", // YES
            "deathgrip", // YES
            //"electricpuddle", //YES
            "empower", // YES
            "firecrackers", // YES
            //"forestallies", // NEED DESCRIPTION
            //"fungaloutbreak", // ENEMIES?
            "heavenlyarmaments", // YES
            //"heavyweaponry", // MAKE IT WORK
            "hexproof", // YES
            //"holynight", // FIX
            //"holypuddle", // YES
            //"hypotermia", // "YOU" PLAY A CARD
            //"icypuddle", // YES
            "ironclad", // YES
            "lanternfestival", // YES
            "lavabursts", // YES
            //"lavapuddle", // YES
            "livingforest", // YES
            //"lonelyblob", // YES
            //"meatfeast", // NEED DESCRIPTION
            //"melancholy",
            //"metalpuddle", // YES
            //"mysticnight", // FIX
            //"noxiousparasites",
            //"pacifism",
            "poisonfields", // YES
            "putrefaction", // YES
            //"resurrection",
            //"revenge", // "YOU" PLAY A CARD
            "rosegarden", // YES
            "sacredground", // YES
            //"snowfall",
            //"spookynight", // FIX
            //"starrynight", // FIX
            "subzero", // YES
            //"sugarrush", // INTO DECK
            "thegrinch", // YES
            //"thornproliferation", NEED DESCRIPTION
            //"threedragons", // NEED DESCRIPTION
            //"threeghost", // NEED DESCRIPTION AND DOESNT WORK
            "thunderstorm", // YES
            //"toxicpuddle", // YES
            //"trickortreat", // NEED DESCRIPTION
            "upwind", // YES
            "vigorous", // YES
            //"waterpuddle", // YES
            //"windsofamnesia"
        ];

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Globals), nameof(Globals.GetExperienceByLevel))]
        public static void GetExperienceByLevel(ref Globals __instance, int level, ref int __result)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;

            if (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_allow_additional_levels")))
            {
                if (level == 5)
                    __result = 2250;
                if (level == 6)
                    __result = 3000;
                if (level == 7)
                    __result = 4000;
                if (level == 8)
                    __result = 6000;
                if (level >= 9)
                    __result = 99999;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), nameof(Character.CalculateRewardForCharacter))]
        public static void CalculateRewardForCharacter(ref Character __instance, ref int _experience, ref int __result)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;

            if (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_allow_additional_levels")))
            {
                int townTier = AtOManager.Instance.GetActNumberForText() - 1;
                if (townTier >= 5)
                {
                    if (AtOManager.Instance.IsChallengeTraitActive("smartheroes"))
                        _experience += Functions.FuncRoundToInt(_experience * 0.5f);

                    float num1 = 0.1f;
                    if (__instance.Level > AtOManager.Instance.GetActNumberForText())
                    {
                        __result = Functions.FuncRoundToInt(_experience * num1);
                        return;
                    }

                    if (__instance.Experience >= Globals.Instance.GetExperienceByLevel(__instance.Level))
                    {
                        __result = Functions.FuncRoundToInt(_experience * num1);
                        return;
                    }

                    if (__instance.Experience + _experience > Globals.Instance.GetExperienceByLevel(__instance.Level))
                    {
                        int num2 = _experience - (Globals.Instance.GetExperienceByLevel(__instance.Level) - __instance.Experience);
                        __result = Globals.Instance.GetExperienceByLevel(__instance.Level) - __instance.Experience + Functions.FuncRoundToInt((float)num2 * num1);
                        return;
                    }

                    __result = _experience;
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(OverCharacter), nameof(OverCharacter.DoStats))]
        public static void DoStats(ref OverCharacter __instance, ref Hero ___hero)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            if (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_allow_additional_levels")))
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append("L");
                stringBuilder.Append(___hero.Level);
                if (___hero.Level < 9)
                {
                    stringBuilder.Append("  <voffset=.15><size=-.5><color=#FFC086>[");
                    stringBuilder.Append(___hero.Experience);
                    stringBuilder.Append("/");
                    stringBuilder.Append(Globals.Instance.GetExperienceByLevel(___hero.Level));
                    stringBuilder.Append("]");
                }
                __instance.experienceText.text = stringBuilder.ToString();
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Globals), nameof(Globals.CreateGameContent))]
        public static void CreateGameContent(ref Globals __instance,
        ref Dictionary<string, EventData> ____Events,
        ref Dictionary<string, NodeData> ____NodeDataSource,
        ref Dictionary<string, CombatData> ____CombatDataSource,
        ref Dictionary<string, CinematicData> ____Cinematics,
        ref Dictionary<string, EventRequirementData> ____Requirements,
        ref Dictionary<string, CardData> ____CardsSource,
        ref Dictionary<string, CardData> ____Cards,
        ref Dictionary<Enums.CardType, List<string>> ____CardItemByType,
        ref Dictionary<Enums.CardType, List<string>> ____CardListByType,
        ref Dictionary<Enums.CardClass, List<string>> ____CardListByClass,
        ref List<string> ____CardListNotUpgraded,
        ref Dictionary<Enums.CardClass, List<string>> ____CardListNotUpgradedByClass,
        ref Dictionary<string, List<string>> ____CardListByClassType,
        ref Dictionary<string, int> ____CardEnergyCost
        )
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;

            foreach (string cardId in blessingCards)
            {
                CardData blessing = CloneBlessingCard(cardId, true, ref ____CardsSource, ref ____Cards, ref ____CardItemByType, ref ____CardListByType, ref ____CardListByClass, ref ____CardListNotUpgraded, ref ____CardListNotUpgradedByClass, ref ____CardListByClassType, ref ____CardEnergyCost);
                availableBlessings.Add(blessing.Id);
            }

            //AddNewBlessings(ref ____CardsSource, ref ____Cards, ref ____CardItemByType, ref ____CardListByType, ref ____CardListByClass, ref ____CardListNotUpgraded, ref ____CardListNotUpgradedByClass, ref ____CardListByClassType, ref ____CardEnergyCost);

            GameManager.Instance.cardSprites = GameManager.Instance.cardSprites.Concat(GameManager.Instance.cardSprites.Where(c => c.name == "card-bg-special")).ToArray();

            AddNewRequirement("endless_complete_sen", ref ____Requirements);
            AddNewRequirement("endless_complete_faen", ref ____Requirements);
            AddNewRequirement("endless_complete_aqua", ref ____Requirements);
            AddNewRequirement("endless_complete_ulmin", ref ____Requirements);
            AddNewRequirement("endless_complete_velka", ref ____Requirements);
            AddNewRequirement("endless_complete_sahti", ref ____Requirements);
            AddNewRequirement("endless_complete_void", ref ____Requirements);

            AddNewRequirement("endless_unique_zones", ref ____Requirements);
            AddNewRequirement("endless_allow_repeats", ref ____Requirements);
            AddNewRequirement("endless_require_all_before_void", ref ____Requirements);

            AddNewRequirement("endless_allow_perks", ref ____Requirements);

            AddNewRequirement("endless_allow_blessings", ref ____Requirements);
            AddNewRequirement("endless_allow_blessings_starting_4", ref ____Requirements);
            AddNewRequirement("endless_allow_blessings_every_4", ref ____Requirements);
            AddNewRequirement("endless_allow_blessings_after_void", ref ____Requirements);
            AddNewRequirement("endless_pick_blessing", ref ____Requirements);

            AddNewRequirement("endless_allow_additional_levels", ref ____Requirements);

            AddNewRequirement("endless_zonecount_1", ref ____Requirements);
            AddNewRequirement("endless_zonecount_2", ref ____Requirements);
            AddNewRequirement("endless_zonecount_3", ref ____Requirements);


            if (____Events.TryGetValue("e_sen44_a", out EventData eventConfigPrefab))
            {
                EventData eventDataAllowPerks = UnityEngine.Object.Instantiate(eventConfigPrefab);
                EventData eventDataAllowBlessings = UnityEngine.Object.Instantiate(eventConfigPrefab);
                EventData eventDataAllowAdditionalLevels = UnityEngine.Object.Instantiate(eventConfigPrefab);
                EventData eventDataRequireAll = UnityEngine.Object.Instantiate(eventConfigPrefab);
                EventData eventDataUniqueZones = UnityEngine.Object.Instantiate(eventConfigPrefab);
                EventData eventDataAllowRepeats = UnityEngine.Object.Instantiate(eventConfigPrefab);
                EventData eventDataZoneCount = UnityEngine.Object.Instantiate(eventConfigPrefab);

                EventReplyData configReplyPrefab = Globals.Instance.GetEventData("e_sen44_a").Replys.First();


                EventReplyData configReplyAllowPerksYes = configReplyPrefab.ShallowCopy();
                EventReplyData configReplyAllowPerksNo = configReplyPrefab.ShallowCopy();

                configReplyAllowPerksYes.ReplyActionText = Enums.EventAction.None;
                configReplyAllowPerksYes.ReplyText = "Yes";
                configReplyAllowPerksYes.SsRewardText = "";
                configReplyAllowPerksYes.SsRequirementUnlock = Globals.Instance.GetRequirementData("endless_allow_perks");
                configReplyAllowPerksYes.SsEvent = eventDataAllowBlessings;

                configReplyAllowPerksNo.ReplyActionText = Enums.EventAction.None;
                configReplyAllowPerksNo.ReplyText = "No";
                configReplyAllowPerksNo.SsRewardText = "";
                configReplyAllowPerksNo.SsRequirementUnlock = null;
                configReplyAllowPerksNo.SsEvent = eventDataAllowBlessings;


                EventReplyData configReplyAllowBlessings = configReplyPrefab.ShallowCopy();
                EventReplyData configReplyAllowBlessingsStarting4 = configReplyPrefab.ShallowCopy();
                EventReplyData configReplyAllowBlessingsEvery4 = configReplyPrefab.ShallowCopy();
                EventReplyData configReplyAllowBlessingsAfterVoid = configReplyPrefab.ShallowCopy();
                EventReplyData configReplyAllowBlessingsNo = configReplyPrefab.ShallowCopy();

                configReplyAllowBlessings.ReplyActionText = Enums.EventAction.None;
                configReplyAllowBlessings.ReplyText = "After every Act";
                configReplyAllowBlessings.SsRewardText = "";
                configReplyAllowBlessings.SsRequirementUnlock = Globals.Instance.GetRequirementData("endless_allow_blessings");
                configReplyAllowBlessings.SsRequirementUnlock2 = Globals.Instance.GetRequirementData("endless_pick_blessing");
                configReplyAllowBlessings.SsEvent = eventDataAllowAdditionalLevels;

                configReplyAllowBlessingsStarting4.ReplyActionText = Enums.EventAction.None;
                configReplyAllowBlessingsStarting4.ReplyText = "After every Act starting at Act 4";
                configReplyAllowBlessingsStarting4.SsRewardText = "";
                configReplyAllowBlessingsStarting4.SsRequirementUnlock = Globals.Instance.GetRequirementData("endless_allow_blessings_starting_4");
                configReplyAllowBlessingsStarting4.SsEvent = eventDataAllowAdditionalLevels;

                configReplyAllowBlessingsEvery4.ReplyActionText = Enums.EventAction.None;
                configReplyAllowBlessingsEvery4.ReplyText = "After every 4th Act";
                configReplyAllowBlessingsEvery4.SsRewardText = "";
                configReplyAllowBlessingsEvery4.SsRequirementUnlock = Globals.Instance.GetRequirementData("endless_allow_blessings_every_4");
                configReplyAllowBlessingsEvery4.SsEvent = eventDataAllowAdditionalLevels;

                configReplyAllowBlessingsAfterVoid.ReplyActionText = Enums.EventAction.None;
                configReplyAllowBlessingsAfterVoid.ReplyText = "After every Void Act";
                configReplyAllowBlessingsAfterVoid.SsRewardText = "";
                configReplyAllowBlessingsAfterVoid.SsRequirementUnlock = Globals.Instance.GetRequirementData("endless_allow_blessings_after_void");
                configReplyAllowBlessingsAfterVoid.SsEvent = eventDataAllowAdditionalLevels;

                configReplyAllowBlessingsNo.ReplyActionText = Enums.EventAction.None;
                configReplyAllowBlessingsNo.ReplyText = "Disable";
                configReplyAllowBlessingsNo.SsRewardText = "";
                configReplyAllowBlessingsNo.SsRequirementUnlock = null;
                configReplyAllowBlessingsNo.SsEvent = eventDataAllowAdditionalLevels;


                EventReplyData configReplyAllowAdditionalLevelsYes = configReplyPrefab.ShallowCopy();
                EventReplyData configReplyAllowAdditionalLevelsNo = configReplyPrefab.ShallowCopy();

                configReplyAllowAdditionalLevelsYes.ReplyActionText = Enums.EventAction.None;
                configReplyAllowAdditionalLevelsYes.ReplyText = "Yes";
                configReplyAllowAdditionalLevelsYes.SsRewardText = "";
                configReplyAllowAdditionalLevelsYes.SsRequirementUnlock = Globals.Instance.GetRequirementData("endless_allow_additional_levels");
                configReplyAllowAdditionalLevelsYes.SsEvent = eventDataRequireAll;

                configReplyAllowAdditionalLevelsNo.ReplyActionText = Enums.EventAction.None;
                configReplyAllowAdditionalLevelsNo.ReplyText = "No";
                configReplyAllowAdditionalLevelsNo.SsRewardText = "";
                configReplyAllowAdditionalLevelsNo.SsRequirementUnlock = null;
                configReplyAllowAdditionalLevelsNo.SsEvent = eventDataRequireAll;


                EventReplyData configReplyRequireAllYes = configReplyPrefab.ShallowCopy();
                EventReplyData configReplyRequireAllNo = configReplyPrefab.ShallowCopy();

                configReplyRequireAllYes.ReplyActionText = Enums.EventAction.None;
                configReplyRequireAllYes.ReplyText = "Yes";
                configReplyRequireAllYes.SsRewardText = "";
                configReplyRequireAllYes.SsRequirementUnlock = Globals.Instance.GetRequirementData("endless_require_all_before_void");
                configReplyRequireAllYes.SsEvent = eventDataUniqueZones;

                configReplyRequireAllNo.ReplyActionText = Enums.EventAction.None;
                configReplyRequireAllNo.ReplyText = "No";
                configReplyRequireAllNo.SsRewardText = "";
                configReplyRequireAllNo.SsRequirementUnlock = null;
                configReplyRequireAllNo.SsEvent = eventDataUniqueZones;


                EventReplyData configReplyUniqueZonesYes = configReplyPrefab.ShallowCopy();
                EventReplyData configReplyUniqueZonesNo = configReplyPrefab.ShallowCopy();

                configReplyUniqueZonesYes.ReplyActionText = Enums.EventAction.None;
                configReplyUniqueZonesYes.ReplyText = "Yes";
                configReplyUniqueZonesYes.SsRewardText = "";
                configReplyUniqueZonesYes.SsRequirementUnlock = Globals.Instance.GetRequirementData("endless_unique_zones");
                configReplyUniqueZonesYes.SsEvent = eventDataZoneCount;

                configReplyUniqueZonesNo.ReplyActionText = Enums.EventAction.None;
                configReplyUniqueZonesNo.ReplyText = "No";
                configReplyUniqueZonesNo.SsRewardText = "";
                configReplyUniqueZonesNo.SsRequirementUnlock = null;
                configReplyUniqueZonesNo.SsEvent = eventDataAllowRepeats;



                EventReplyData configReplyAllowRepeatsYes = configReplyPrefab.ShallowCopy();
                EventReplyData configReplyAllowRepeatsNo = configReplyPrefab.ShallowCopy();

                configReplyAllowRepeatsYes.ReplyActionText = Enums.EventAction.None;
                configReplyAllowRepeatsYes.ReplyText = "Yes";
                configReplyAllowRepeatsYes.SsRewardText = "";
                configReplyAllowRepeatsYes.SsRequirementUnlock = Globals.Instance.GetRequirementData("endless_allow_repeats");
                configReplyAllowRepeatsYes.SsEvent = eventDataZoneCount;

                configReplyAllowRepeatsNo.ReplyActionText = Enums.EventAction.None;
                configReplyAllowRepeatsNo.ReplyText = "No";
                configReplyAllowRepeatsNo.SsRewardText = "";
                configReplyAllowRepeatsNo.SsRequirementUnlock = null;
                configReplyAllowRepeatsNo.SsEvent = eventDataZoneCount;



                EventReplyData configReplyZoneCount1 = configReplyPrefab.ShallowCopy();
                EventReplyData configReplyZoneCount2 = configReplyPrefab.ShallowCopy();
                EventReplyData configReplyZoneCount3 = configReplyPrefab.ShallowCopy();
                EventReplyData configReplyZoneCountAll = configReplyPrefab.ShallowCopy();

                configReplyZoneCount1.ReplyActionText = Enums.EventAction.None;
                configReplyZoneCount1.ReplyText = "1";
                configReplyZoneCount1.SsRewardText = "";
                configReplyZoneCount1.SsRequirementUnlock = Globals.Instance.GetRequirementData("endless_zonecount_1");
                configReplyZoneCount1.SsEvent = null;

                configReplyZoneCount2.ReplyActionText = Enums.EventAction.None;
                configReplyZoneCount2.ReplyText = "2";
                configReplyZoneCount2.SsRewardText = "";
                configReplyZoneCount2.SsRequirementUnlock = Globals.Instance.GetRequirementData("endless_zonecount_2");
                configReplyZoneCount2.SsEvent = null;

                configReplyZoneCount3.ReplyActionText = Enums.EventAction.None;
                configReplyZoneCount3.ReplyText = "3";
                configReplyZoneCount3.SsRewardText = "";
                configReplyZoneCount3.SsRequirementUnlock = Globals.Instance.GetRequirementData("endless_zonecount_3");
                configReplyZoneCount3.SsEvent = null;

                configReplyZoneCountAll.ReplyActionText = Enums.EventAction.None;
                configReplyZoneCountAll.ReplyText = "All";
                configReplyZoneCountAll.SsRewardText = "";
                configReplyZoneCountAll.SsRequirementUnlock = null;
                configReplyZoneCountAll.SsEvent = null;



                eventDataAllowPerks.EventName = "Endless Perks";
                eventDataAllowPerks.Description = "At the end of each act you will be given a list of random perks to choose from. These perks are permanent and will last until the end of the run. These are purely stat based.";
                eventDataAllowPerks.DescriptionAction = "Enable randomized perks at the end of each act?";
                eventDataAllowPerks.EventId = "e_endless_allow_perks";
                eventDataAllowPerks.Replys = [configReplyAllowPerksYes, configReplyAllowPerksNo];
                eventDataAllowPerks.Init();
                ____Events.Add(eventDataAllowPerks.EventId.ToLower(), eventDataAllowPerks);

                eventDataAllowBlessings.EventName = "Endless Blessings";
                eventDataAllowBlessings.Description = "At the end of certain acts you will be given a list of random blessings to choose from. These blessings are permanent and will last until the end of the run. These have in-combat effects, similar to corruptions.";
                eventDataAllowBlessings.DescriptionAction = "Enable randomized blessings at the end of acts?";
                eventDataAllowBlessings.EventId = "e_endless_allow_blessings";
                eventDataAllowBlessings.Replys = [configReplyAllowBlessings, configReplyAllowBlessingsStarting4, configReplyAllowBlessingsEvery4, configReplyAllowBlessingsAfterVoid, configReplyAllowBlessingsNo];
                eventDataAllowBlessings.Init();
                ____Events.Add(eventDataAllowBlessings.EventId.ToLower(), eventDataAllowBlessings);

                eventDataAllowAdditionalLevels.EventName = "Unshackled Growth";
                eventDataAllowAdditionalLevels.Description = "Change the character level cap to allow leveling up to level 9. This will allow you to select traits from the other side of the tree.";
                eventDataAllowAdditionalLevels.DescriptionAction = "Enable additional levels?";
                eventDataAllowAdditionalLevels.EventId = "e_endless_allow_additional_levels";
                eventDataAllowAdditionalLevels.Replys = [configReplyAllowAdditionalLevelsYes, configReplyAllowAdditionalLevelsNo];
                eventDataAllowAdditionalLevels.Init();
                ____Events.Add(eventDataAllowAdditionalLevels.EventId.ToLower(), eventDataAllowAdditionalLevels);

                eventDataRequireAll.EventName = "Adventure-like";
                eventDataRequireAll.Description = "This will require you to complete all other acts before being allowed to enter the void act. This is similar to how adventure mode works. Each cycle will restart this requirement.";
                eventDataRequireAll.DescriptionAction = "Require all other acts to be completed per cycle before void act?";
                eventDataRequireAll.EventId = "e_endless_require_all_before_void";
                eventDataRequireAll.Replys = [configReplyRequireAllYes, configReplyRequireAllNo];
                eventDataRequireAll.Init();
                ____Events.Add(eventDataRequireAll.EventId.ToLower(), eventDataRequireAll);

                eventDataUniqueZones.EventName = "Déjà Vu";
                eventDataUniqueZones.Description = "This will cause each zone to only be encountered once per cycle. For example, if you encounter the Faeborg Forest act, you will not be able to encounter it again until you have completed The Void act and started a new cycle.";
                eventDataUniqueZones.DescriptionAction = "Only encounter each zone once per cycle?";
                eventDataUniqueZones.EventId = "e_endless_unique_zones";
                eventDataUniqueZones.Replys = [configReplyUniqueZonesYes, configReplyUniqueZonesNo];
                eventDataUniqueZones.Init();
                ____Events.Add(eventDataUniqueZones.EventId.ToLower(), eventDataUniqueZones);

                eventDataAllowRepeats.EventName = "Déjà Vu... Again?";
                eventDataAllowRepeats.Description = "This will allow the same zone to be encountered after itself. For example, you could encounter the Faeborg Forest act, and then encounter the Faeborg Forest act again.";
                eventDataAllowRepeats.DescriptionAction = "Allow the same zone to be encountered after itself?";
                eventDataAllowRepeats.EventId = "e_endless_allow_repeats";
                eventDataAllowRepeats.Replys = [configReplyAllowRepeatsYes, configReplyAllowRepeatsNo];
                eventDataAllowRepeats.Init();
                ____Events.Add(eventDataAllowRepeats.EventId.ToLower(), eventDataAllowRepeats);

                eventDataZoneCount.EventName = "Oracle's Vision";
                eventDataZoneCount.Description = "Choose how many different zones you can be offered at the obelisk. Choosing 'All' will allow you to be offered any of the remaining zones that you have not completed yet in the current cycle. Choosing 1 will only offer you one random zone.";
                eventDataZoneCount.DescriptionAction = "How many random zones to pick from at the obelisk?";
                eventDataZoneCount.EventId = "e_endless_zone_count";
                eventDataZoneCount.Replys = [configReplyZoneCount1, configReplyZoneCount2, configReplyZoneCount3, configReplyZoneCountAll];
                eventDataZoneCount.Init();
                ____Events.Add(eventDataZoneCount.EventId.ToLower(), eventDataZoneCount);
            }
            Sprite endlessObeliskSprite = LoadSprite("AtOEndless.Assets.endless.png");

            if (____Events.TryGetValue("e_sen34_a", out EventData eventPrefab))
            {
                EventData eventDataEndlessObelisk = UnityEngine.Object.Instantiate<EventData>(eventPrefab);
                eventDataEndlessObelisk.EventName = "Endless Obelisk";
                eventDataEndlessObelisk.Description = "A large floating obelisk stands before you, radiating a strange energy. It seems to be a gateway to other worlds.";
                eventDataEndlessObelisk.DescriptionAction = "Approach the obelisk and envision a world you wish to visit.";
                eventDataEndlessObelisk.EventId = "e_endless_obelisk";
                eventDataEndlessObelisk.Replys = [];
                eventDataEndlessObelisk.EventSpriteBook = endlessObeliskSprite;
                eventDataEndlessObelisk.Init();
                ____Events.Add(eventDataEndlessObelisk.EventId.ToLower(), eventDataEndlessObelisk);
            }

            if (____Events.TryGetValue("e_challenge_next", out EventData perkPrefab))
            {
                EventData eventDataEndlessPerk = UnityEngine.Object.Instantiate<EventData>(perkPrefab);
                eventDataEndlessPerk.EventName = "Power of the Obelisk";
                eventDataEndlessPerk.Description = "As you approach the obelisk you feel a surge of power course through your veins.";
                eventDataEndlessPerk.DescriptionAction = "Channel this power into a permanent perk.";
                eventDataEndlessPerk.EventId = "e_endless_perk";
                eventDataEndlessPerk.Requirement = Globals.Instance.GetRequirementData("endless_allow_perks");
                eventDataEndlessPerk.Replys = [];
                eventDataEndlessPerk.EventSpriteBook = endlessObeliskSprite;
                eventDataEndlessPerk.ReplyRandom = 0;
                eventDataEndlessPerk.Init();
                ____Events.Add(eventDataEndlessPerk.EventId.ToLower(), eventDataEndlessPerk);
            }

            if (____Events.TryGetValue("e_challenge_next", out EventData blessingPrefab))
            {
                EventData eventDataEndlessBlessing = UnityEngine.Object.Instantiate<EventData>(blessingPrefab);
                eventDataEndlessBlessing.EventName = "Blessing of the Obelisk";
                eventDataEndlessBlessing.Description = "Getting closer to the obelisk you feel a strange energy enveloping you, as if the obelisk is blessing you with its power.";
                eventDataEndlessBlessing.DescriptionAction = "Receive a blessing to aid you in your journey.";
                eventDataEndlessBlessing.EventId = "e_endless_blessing";
                eventDataEndlessBlessing.Requirement = Globals.Instance.GetRequirementData("endless_pick_blessing");
                eventDataEndlessBlessing.Replys = [];
                eventDataEndlessBlessing.EventSpriteBook = endlessObeliskSprite;
                eventDataEndlessBlessing.ReplyRandom = 0;
                eventDataEndlessBlessing.Init();
                ____Events.Add(eventDataEndlessBlessing.EventId.ToLower(), eventDataEndlessBlessing);
            }

            if (____Cinematics.TryGetValue("intro", out CinematicData introData))
            {
                introData.CinematicEvent = Globals.Instance.GetEventData("e_endless_allow_perks");
                ____Cinematics["intro"] = introData;
            }

            if (____NodeDataSource.TryGetValue("sen_34", out NodeData sen34Data))
            {
                sen34Data.NodeEvent = [Globals.Instance.GetEventData("e_endless_perk"), Globals.Instance.GetEventData("e_endless_blessing"), Globals.Instance.GetEventData("e_endless_obelisk")];
                sen34Data.NodeEventPriority = [0, 1, 2];
                ____NodeDataSource["sen_34"] = sen34Data;
            }
            if (____NodeDataSource.TryGetValue("faen_39", out NodeData faen39Data))
            {
                faen39Data.NodeEvent = [Globals.Instance.GetEventData("e_endless_perk"), Globals.Instance.GetEventData("e_endless_blessing"), Globals.Instance.GetEventData("e_endless_obelisk")];
                faen39Data.NodeEventPriority = [0, 1, 2];
                ____NodeDataSource["faen_39"] = faen39Data;
            }
            if (____NodeDataSource.TryGetValue("aqua_36", out NodeData aqua36Data))
            {
                aqua36Data.NodeEvent = [Globals.Instance.GetEventData("e_endless_perk"), Globals.Instance.GetEventData("e_endless_blessing"), Globals.Instance.GetEventData("e_endless_obelisk")];
                aqua36Data.NodeEventPriority = [0, 1, 2];
                ____NodeDataSource["aqua_36"] = aqua36Data;
            }
            if (____NodeDataSource.TryGetValue("velka_33", out NodeData velka33Data))
            {
                velka33Data.NodeEvent = [Globals.Instance.GetEventData("e_endless_perk"), Globals.Instance.GetEventData("e_endless_blessing"), Globals.Instance.GetEventData("e_endless_obelisk")];
                velka33Data.NodeEventPriority = [0, 1, 2];
                ____NodeDataSource["velka_33"] = velka33Data;
            }
            if (____NodeDataSource.TryGetValue("ulmin_40", out NodeData ulmin40Data))
            {
                ulmin40Data.NodeEvent = [Globals.Instance.GetEventData("e_endless_perk"), Globals.Instance.GetEventData("e_endless_blessing"), Globals.Instance.GetEventData("e_endless_obelisk")];
                ulmin40Data.NodeEventPriority = [0, 1, 2];
                ____NodeDataSource["ulmin_40"] = ulmin40Data;
            }
            if (____NodeDataSource.TryGetValue("sahti_63", out NodeData sahti63Data))
            {
                sahti63Data.NodeEvent = [Globals.Instance.GetEventData("e_endless_perk"), Globals.Instance.GetEventData("e_endless_blessing"), Globals.Instance.GetEventData("e_endless_obelisk")];
                sahti63Data.NodeEventPriority = [0, 1, 2];
                ____NodeDataSource["sahti_63"] = sahti63Data;
            }
            if (____CombatDataSource.TryGetValue("evoidhigh_13b", out CombatData evoidhigh13bData))
            {
                evoidhigh13bData.EventData = Globals.Instance.GetEventData("e_endless_obelisk");
                ____CombatDataSource["evoidhigh_13b"] = evoidhigh13bData;
            }
            if (____Cinematics.TryGetValue("endgame", out CinematicData endgameData))
            {
                endgameData.CinematicEndAdventure = false;
                ____Cinematics["endgame"] = endgameData;
            }


            GameManager.Instance.DebugShow();
        }

        private static Sprite LoadSprite(string filename)
        {
            Texture2D texture;
            Image img;
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(filename))
            {
                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);
                img = Image.FromStream(stream);
                texture = new Texture2D(img.Width, img.Height);
                texture.LoadImage(data);
            }
            LogInfo($"Loaded {filename} w:{img.Width} h:{img.Height}");
            Sprite mySprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, img.Width, img.Height), new Vector2(0.5f, 0.5f));
            return mySprite;
        }

        public static Dictionary<Enums.Zone, List<string>> RemoveRequirementsByZone = new()
        {
            { Enums.Zone.Senenthia, ["treasurehuntii"] },
            { Enums.Zone.Faeborg, ["darkdeal"] },
            { Enums.Zone.Velkarath, ["armblowtorch", "armchargedrod", "armcoolingengine", "armdisclauncher", "armsmallcannon"]},
            { Enums.Zone.Aquarfall, [] },
            { Enums.Zone.Ulminin, ["ulmininportal", "ulminindown", "riftulmi61"] },
            { Enums.Zone.Sahti, ["sahtidown", "sahtipolizonevent", "sahtiship", "dreadroom", "dreadup", "dreadmastcannon"] },
            { Enums.Zone.VoidLow, [] },
        };

        public static Dictionary<Enums.Zone, List<string>> AddRequirementsByZone = new()
        {
            { Enums.Zone.Senenthia, ["crossroadnorth", "crossroadsouth", "caravannopay", "fungamemill", "riftsen47", "riftsen48"] },
            { Enums.Zone.Faeborg, ["boatfaenlor", "riftfaen42", "riftfaen43"] },
            { Enums.Zone.Velkarath, ["lavacascade", "goblinnorth", "riftvelka39", "riftvelka40"]},
            { Enums.Zone.Aquarfall, ["boatup", "boatcenter", "boatdown", "riftaqua46", "riftaqua47"] },
            { Enums.Zone.Ulminin, ["ulmininup", "riftulmi60", "riftulmi61"] },
            { Enums.Zone.Sahti, ["sahtiup", "dreaddown", "sahtipirateking", "sahtipolizon", "riftsahti67", "riftsahti68"] },
            { Enums.Zone.VoidLow, ["voidnorth", "voidnorthpass", "voidsouth", "voidsouthpass"] },

        };

        [HarmonyPrefix]
        [HarmonyPatch(typeof(EventManager), nameof(EventManager.CloseEvent))]
        public static void CloseEvent(ref EventManager __instance, ref NodeData ___destinationNode, ref EventData ___currentEvent)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            if (___destinationNode != null && ___currentEvent.EventId == "e_endless_obelisk")
            {
                AtOManager.Instance.SetTownTier(AtOManager.Instance.GetActNumberForText());
                AtOManager.Instance.SetGameId($"{AtOManager.Instance.GetGameId().Split('+').First()}+{AtOManager.Instance.GetActNumberForText()}");
                AtOManager.Instance.gameNodeAssigned.Clear();
                AtOManager.Instance.RemoveItemList(true);

                if (RemoveRequirementsByZone.TryGetValue(AtOManager.Instance.GetMapZone(___destinationNode.NodeId), out List<string> requirementsToRemove))
                {
                    foreach (string requirementToRemove in requirementsToRemove)
                    {
                        AtOManager.Instance.RemovePlayerRequirement(Globals.Instance.GetRequirementData(requirementToRemove));
                    }
                }
                if (AddRequirementsByZone.TryGetValue(AtOManager.Instance.GetMapZone(___destinationNode.NodeId), out List<string> requirementsToAdd))
                {
                    foreach (string requirementToAdd in requirementsToAdd)
                    {
                        AtOManager.Instance.AddPlayerRequirement(Globals.Instance.GetRequirementData(requirementToAdd));
                    }
                }
                // BLESSING AFTER EVERY ZONE
                if (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_allow_blessings")))
                {
                    AtOManager.Instance.AddPlayerRequirement(Globals.Instance.GetRequirementData("endless_pick_blessing"));
                }
                // BLESSING AFTER EVERY ZONE STARTING AT 4
                if (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_allow_blessings_starting_4")))
                {
                    if (AtOManager.Instance.GetActNumberForText() > 3)
                    {
                        AtOManager.Instance.AddPlayerRequirement(Globals.Instance.GetRequirementData("endless_pick_blessing"));
                    }
                    else
                    {
                        AtOManager.Instance.RemovePlayerRequirement(Globals.Instance.GetRequirementData("endless_pick_blessing"));
                    }
                }
                // BLESSING AFTER EVERY 4TH ZONE
                if (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_allow_blessings_every_4")))
                {
                    if (AtOManager.Instance.GetActNumberForText() % 4 == 0)
                    {
                        AtOManager.Instance.AddPlayerRequirement(Globals.Instance.GetRequirementData("endless_pick_blessing"));
                    }
                    else
                    {
                        AtOManager.Instance.RemovePlayerRequirement(Globals.Instance.GetRequirementData("endless_pick_blessing"));
                    }
                }
                // BLESSING AFTER VOID ZONES
                if (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_allow_blessings_after_void")))
                {
                    if (AtOManager.Instance.GetMapZone(___destinationNode.NodeId) == Enums.Zone.VoidLow)
                    {
                        AtOManager.Instance.AddPlayerRequirement(Globals.Instance.GetRequirementData("endless_pick_blessing"));
                    }
                    else
                    {
                        AtOManager.Instance.RemovePlayerRequirement(Globals.Instance.GetRequirementData("endless_pick_blessing"));
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(AtOManager), nameof(AtOManager.AddPlayerRequirement))]
        public static void AddPlayerRequirement(ref AtOManager __instance, EventRequirementData requirement, bool share, ref List<string> ___playerRequeriments)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            LogInfo($"Add Requirement: {requirement.RequirementId}");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(AtOManager), nameof(AtOManager.RemovePlayerRequirement))]
        public static void RemovePlayerRequirement(ref AtOManager __instance, EventRequirementData requirement, string requirementId, ref List<string> ___playerRequeriments)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            LogInfo($"Remove Requirement: {(requirementId != "" ? requirementId : requirement.RequirementId)}");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AtOManager), nameof(AtOManager.UpgradeTownTier))]
        public static bool UpgradeTownTier(ref AtOManager __instance, ref int ___townTier)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return true;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NPC), nameof(NPC.InitData))]
        public static void InitDataPre(ref NPC __instance)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            if (__instance.NpcData != null && AtOManager.Instance.GetActNumberForText() >= 3 && __instance.NpcData.UpgradedMob != null)
                __instance.NpcData = __instance.NpcData.UpgradedMob;
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(NPC), nameof(NPC.InitData))]
        public static void InitDataPost(ref NPC __instance)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            int townTier = AtOManager.Instance.GetActNumberForText() - 1;
            if (townTier >= 1 && (AtOManager.Instance.GetMapZone(AtOManager.Instance.currentMapNode) == Enums.Zone.Senenthia || AtOManager.Instance.GetMapZone(AtOManager.Instance.currentMapNode) == Enums.Zone.Sectarium))
            {
                __instance.Hp = __instance.HpCurrent = Functions.FuncRoundToInt(__instance.Hp + (__instance.Hp * 3f));
                __instance.Speed += 2;
            }

            if (townTier >= 3 && AtOManager.Instance.GetMapZone(AtOManager.Instance.currentMapNode) != Enums.Zone.VoidLow && AtOManager.Instance.GetMapZone(AtOManager.Instance.currentMapNode) != Enums.Zone.VoidHigh)
            {
                __instance.Hp = __instance.HpCurrent = Functions.FuncRoundToInt(__instance.Hp + (__instance.Hp * 0.5f));
                __instance.Speed += 1;
            }

            if (townTier >= 4)
            {
                __instance.Hp = __instance.HpCurrent = Functions.FuncRoundToInt(__instance.Hp + (__instance.Hp * (0.5f * (townTier - 3))));
                __instance.Speed += 1 * (townTier - 3);
            }
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), nameof(Character.GetAuraCurseQuantityModification))]
        public static void GetAuraCurseQuantityModification(ref Character __instance, ref int __result, string id, Enums.CardClass CC, ref bool ___isHero)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            if (!___isHero)
            {
                if (id == "doom" || id == "paralyze" || id == "invulnerable" || id == "stress" || id == "fatigue")
                    return;

                int townTier = AtOManager.Instance.GetActNumberForText() - 1;
                if (townTier >= 4)
                {
                    __result = Mathf.FloorToInt(__result + (1 + (.50f * (townTier - 3))));
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), nameof(Character.DamageBonus))]
        public static void DamageBonus(ref Character __instance, ref float[] __result,
        Enums.DamageType DT, int energyCost, ref bool ___isHero)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            if (!___isHero)
            {
                int townTier = AtOManager.Instance.GetActNumberForText() - 1;
                if (townTier >= 4)
                {
                    __result = [
                        __result[0] + (2 * (townTier - 3)),
                        __result[1] + (15f * (townTier - 3)),
                    ];
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), nameof(Character.HealBonus))]
        public static void HealBonus(ref Character __instance, ref float[] __result,
        int energyCost, ref bool ___isHero)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            if (!___isHero)
            {
                int townTier = AtOManager.Instance.GetActNumberForText() - 1;
                if (townTier >= 4)
                {
                    __result = [
                        __result[0] + (2 * (townTier - 3)),
                        __result[1] + (15f * (townTier - 3)),
                    ];
                }
            }
        }



        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameData), nameof(GameData.FillData))]
        public static void FillData(ref GameData __instance, ref int ___townTier)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            ___townTier = AtOManager.Instance.GetActNumberForText() - 1;
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(AtOManager), nameof(AtOManager.SetCurrentNode))]
        public static void SetCurrentNodePre(ref AtOManager __instance, out string[][] __state)
        {
            __state = [[.. AtOManager.Instance.mapVisitedNodes], [.. AtOManager.Instance.mapVisitedNodesAction]];
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            AtOManager.Instance.mapVisitedNodes.Clear();
            AtOManager.Instance.mapVisitedNodesAction.Clear();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(AtOManager), nameof(AtOManager.SetCurrentNode))]
        public static void SetCurrentNodePost(ref AtOManager __instance, string[][] __state)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            AtOManager.Instance.mapVisitedNodes.AddRange([.. __state[0]]);
            AtOManager.Instance.mapVisitedNodesAction.AddRange([.. __state[1]]);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MapManager), "DrawNodes")]
        public static void DrawNodesPre(ref MapManager __instance, out string[][] __state)
        {
            __state = [[.. AtOManager.Instance.mapVisitedNodes], [.. AtOManager.Instance.mapVisitedNodesAction]];
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            AtOManager.Instance.mapVisitedNodes.Clear();
            AtOManager.Instance.mapVisitedNodesAction.Clear();
        }

        public static string[] zoneStartNodes = ["sen_0", "faen_0", "aqua_0", "velka_0", "ulmin_0", "sahti_0", "voidlow_0"];

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MapManager), "DrawNodes")]
        public static void DrawNodesPost(ref MapManager __instance, string[][] __state, ref Dictionary<string, Node> ___mapNode)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            AtOManager.Instance.mapVisitedNodes.AddRange([.. __state[0]]);
            AtOManager.Instance.mapVisitedNodesAction.AddRange([.. __state[1]]);

            for (int index = 0; index < AtOManager.Instance.mapVisitedNodes.Count; ++index)
            {
                if (AtOManager.Instance.mapVisitedNodes[index] != "" && ___mapNode.ContainsKey(AtOManager.Instance.mapVisitedNodes[index]))
                {
                    ___mapNode[AtOManager.Instance.mapVisitedNodes[index]].SetVisited();
                }
                if (zoneStartNodes.Contains(AtOManager.Instance.mapVisitedNodes[index]))
                {
                    break;
                }
            }
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(MapManager), "GetMapNodesCo")]
        public static void GetMapNodesCoPre(ref MapManager __instance, out string[][] __state)
        {
            __state = [[.. AtOManager.Instance.mapVisitedNodes], [.. AtOManager.Instance.mapVisitedNodesAction]];
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            AtOManager.Instance.mapVisitedNodes.Clear();
            AtOManager.Instance.mapVisitedNodesAction.Clear();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MapManager), "GetMapNodesCo")]
        public static void GetMapNodesCoPost(ref MapManager __instance, string[][] __state)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            AtOManager.Instance.mapVisitedNodes.AddRange([.. __state[0]]);
            AtOManager.Instance.mapVisitedNodesAction.AddRange([.. __state[1]]);
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(MapManager), "AssignGameNodes")]
        public static void AssignGameNodesPre(ref MapManager __instance, out string[][] __state)
        {
            __state = [[.. AtOManager.Instance.mapVisitedNodes], [.. AtOManager.Instance.mapVisitedNodesAction]];
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            AtOManager.Instance.mapVisitedNodes.Clear();
            AtOManager.Instance.mapVisitedNodesAction.Clear();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MapManager), "AssignGameNodes")]
        public static void AssignGameNodesPost(ref MapManager __instance, string[][] __state)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            AtOManager.Instance.mapVisitedNodes.AddRange([.. __state[0]]);
            AtOManager.Instance.mapVisitedNodesAction.AddRange([.. __state[1]]);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CinematicManager), "DoCinematic")]
        public static bool DoCinematicPre(ref CinematicManager __instance, ref CinematicData ___cinematicData)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return true;
            if (AtOManager.Instance.CinematicId == "intro" && AtOManager.Instance.GetActNumberForText() > 1)
            {
                GameManager.Instance.ChangeScene("Map");
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AtOManager), nameof(AtOManager.FinishGame))]
        public static bool FinishGame(ref AtOManager __instance, ref CombatData ___currentCombatData)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return true;
            if (___currentCombatData.CombatId == "evoidhigh_13b")
            {
                return false;
            }
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MapManager), "SetPositionInCurrentNode")]
        public static void SetPositionInCurrentNode(ref MapManager __instance, ref bool __result)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            if (__result == false && AtOManager.Instance.currentMapNode == "voidhigh_13" && AtOManager.Instance.bossesKilledName.Any(s => s.StartsWith("archonnihr", StringComparison.OrdinalIgnoreCase)))
            {
                CombatData currentCombatData = AtOManager.Instance.GetCurrentCombatData();
                CombatData globalCombatData = Globals.Instance.GetCombatData(currentCombatData?.CombatId);
                if (currentCombatData != globalCombatData)
                {
                    AtOManager.Instance.SetCombatData(globalCombatData);
                    currentCombatData = AtOManager.Instance.GetCurrentCombatData();
                }

                if (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_allow_perks")))
                {
                    if (currentCombatData.EventData != Globals.Instance.GetEventData("e_endless_perk"))
                    {
                        currentCombatData.EventData = Globals.Instance.GetEventData("e_endless_perk");
                    }
                }
                else if (AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_allow_blessings")) &&
                AtOManager.Instance.PlayerHasRequirement(Globals.Instance.GetRequirementData("endless_pick_blessing")))
                {
                    if (currentCombatData.EventData != Globals.Instance.GetEventData("e_endless_blessing"))
                    {
                        currentCombatData.EventData = Globals.Instance.GetEventData("e_endless_blessing");
                    }
                }
                __result = true;
                return;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(IntroNewGameManager), "DoIntro")]
        public static void DoIntro(ref IntroNewGameManager __instance)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            __instance.title.text = __instance.title.text.Replace(string.Format(Texts.Instance.GetText("actNumber"), (AtOManager.Instance.GetTownTier() + 2)), string.Format(Texts.Instance.GetText("actNumber"), AtOManager.Instance.GetActNumberForText()));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MenuSaveButton), nameof(MenuSaveButton.SetGameData))]
        public static void SetGameData(ref MenuSaveButton __instance, GameData _gameData)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            __instance.descriptionText.text = __instance.descriptionText.text.Replace(string.Format(Texts.Instance.GetText("actNumber"), Math.Min(4, _gameData.TownTier + 1)), string.Format(Texts.Instance.GetText("actNumber"), _gameData.TownTier + 1));
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.SetScore))]
        public static bool SetScore()
        {
            return false;
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.SetObeliskScore))]
        public static bool SetObeliskScore()
        {
            return false;
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.SetSingularityScore))]
        public static bool SetSingularityScore()
        {
            return false;
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.SetWeeklyScore))]
        public static bool SetWeeklyScore()
        {
            return false;
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MatchManager), nameof(MatchManager.CastCardAction))]
        public static void CastCardAction(MatchManager __instance,
                                            ref CardData _cardActive,
                                            Transform targetTransformCast,
                                            ref CardItem theCardItem,
                                            string _uniqueCastId,
                                            bool _automatic,
                                            ref CardData _card,
                                            int _cardIterationTotal,
                                            int _cardSpecialValueGlobal)
        {
            if (!_automatic)
            {
                if (theCardItem.CardData.KillPet)
                    theCardItem.CardData.KillPet = false;
            }
            else if (_cardActive == null && _card != null)
            {
                if (_card.KillPet)
                    _card.KillPet = false;
            }
            else
            {
                if (_cardActive.KillPet)
                    _cardActive.KillPet = false;
            }
        }





        [HarmonyPrefix]
        [HarmonyPatch(typeof(Character), "AuraCurseImmunitiesByItemsList")]
        public static void AuraCurseImmunitiesByItemsListPre(ref Character __instance)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "AuraCurseImmunitiesByItemsList")]
        public static void AuraCurseImmunitiesByItemsListPost(ref Character __instance, ref List<string> __result, ref HeroData ___heroData, ref string ___subclassName)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;

            if (___heroData != null)
            {
                foreach (string blessing in activeBlessings)
                {
                    CardData blessingCard = Globals.Instance.GetCardData(blessing);
                    if (blessingCard != null && blessingCard.Item != null)
                    {
                        if (blessingCard.Item.AuracurseImmune1 != null && !__result.Contains(blessingCard.Item.AuracurseImmune1.Id))
                            __result.Add(blessingCard.Item.AuracurseImmune1.Id);
                        if (blessingCard.Item.AuracurseImmune2 != null && !__result.Contains(blessingCard.Item.AuracurseImmune2.Id))
                            __result.Add(blessingCard.Item.AuracurseImmune2.Id);
                    }
                }
                if (__result.Contains("bleed") && AtOManager.Instance.CharacterHavePerk(___subclassName, "mainperkfury1c"))
                    __result.Remove("bleed");
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Character), "AuraCurseImmuneByItems")]
        public static void AuraCurseImmuneByItemsPre(ref Character __instance, string acName, out bool __state, ref bool ___useCache, ref Dictionary<string, bool> ___cacheAuraCurseImmuneByItems)
        {
            __state = false;
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;

            if ((bool)MatchManager.Instance && ___useCache && ___cacheAuraCurseImmuneByItems.ContainsKey(acName))
                __state = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "AuraCurseImmuneByItems")]
        public static void AuraCurseImmuneByItemsPost(ref Character __instance, string acName, bool __state, ref bool __result, ref bool ___useCache, ref Dictionary<string, bool> ___cacheAuraCurseImmuneByItems, ref HeroData ___heroData, ref string ___subclassName)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            if (__state)
                return;
            if (__result == false && acName == "bleed" && AtOManager.Instance.CharacterHavePerk(___subclassName, "mainperkfury1c"))
                return;

            if (___heroData != null)
            {
                foreach (string blessing in activeBlessings)
                {
                    CardData blessingCard = Globals.Instance.GetCardData(blessing);
                    if (blessingCard != null && blessingCard.Item != null)
                    {
                        if (blessingCard.Item.AuracurseImmune1 != null && blessingCard.Item.AuracurseImmune1.Id == acName)
                        {
                            if (___cacheAuraCurseImmuneByItems.ContainsKey(acName))
                                ___cacheAuraCurseImmuneByItems[acName] = true;
                            else
                                ___cacheAuraCurseImmuneByItems.Add(acName, true);
                            __result = true;
                            return;
                        }
                        if (blessingCard.Item.AuracurseImmune2 != null && blessingCard.Item.AuracurseImmune2.Id == acName)
                        {
                            if (___cacheAuraCurseImmuneByItems.ContainsKey(acName))
                                ___cacheAuraCurseImmuneByItems[acName] = true;
                            else
                                ___cacheAuraCurseImmuneByItems.Add(acName, true);
                            __result = true;
                            return;
                        }
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "GetItemsMaxHPModifier")]
        public static void GetItemsMaxHPModifierPost(ref Character __instance, ref int __result, ref HeroData ___heroData, ref string ___subclassName)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;

            if (___heroData != null)
            {
                foreach (string blessing in activeBlessings)
                {
                    CardData blessingCard = Globals.Instance.GetCardData(blessing);
                    if (blessingCard != null && blessingCard.Item != null && blessingCard.Item.MaxHealth != 0)
                    {
                        __result += blessingCard.Item.MaxHealth;
                    }
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Character), "GetItemHealFlatBonus")]
        public static void GetItemHealFlatBonusPre(ref Character __instance, out bool __state, ref bool ___useCache, ref List<int> ___cacheGetItemHealFlatBonus)
        {
            __state = false;
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;

            if ((bool)MatchManager.Instance && ___useCache && ___cacheGetItemHealFlatBonus.Count > 0)
                __state = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "GetItemHealFlatBonus")]
        public static void GetItemHealFlatBonusPost(ref Character __instance, bool __state, ref int __result, ref bool ___useCache, ref List<int> ___cacheGetItemHealFlatBonus, ref HeroData ___heroData, ref string ___subclassName)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            if (__state)
                return;

            if (___heroData != null)
            {
                bool modified = false;
                foreach (string blessing in activeBlessings)
                {
                    CardData blessingCard = Globals.Instance.GetCardData(blessing);
                    if (blessingCard != null && blessingCard.Item != null && blessingCard.Item.HealFlatBonus != 0)
                    {
                        __result += blessingCard.Item.HealFlatBonus;
                        modified = true;
                    }
                }
                if ((bool)MatchManager.Instance && ___useCache && ___cacheGetItemHealFlatBonus.Count > 0 && modified)
                    ___cacheGetItemHealFlatBonus[0] = __result;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Character), "GetItemHealPercentBonus")]
        public static void GetItemHealPercentBonusPre(ref Character __instance, out bool __state, ref bool ___useCache, ref List<float> ___cacheGetItemHealPercentBonus)
        {
            __state = false;
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;

            if ((bool)MatchManager.Instance && ___useCache && ___cacheGetItemHealPercentBonus.Count > 0)
                __state = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "GetItemHealPercentBonus")]
        public static void GetItemHealPercentBonusPost(ref Character __instance, bool __state, ref float __result, ref bool ___useCache, ref List<float> ___cacheGetItemHealPercentBonus, ref HeroData ___heroData, ref string ___subclassName)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            if (__state)
                return;

            if (___heroData != null)
            {
                bool modified = false;
                foreach (string blessing in activeBlessings)
                {
                    CardData blessingCard = Globals.Instance.GetCardData(blessing);
                    if (blessingCard != null && blessingCard.Item != null && (double)blessingCard.Item.HealPercentBonus != 0.0)
                    {
                        __result += blessingCard.Item.HealPercentBonus;
                        modified = true;
                    }
                }
                if ((bool)MatchManager.Instance && ___useCache && ___cacheGetItemHealPercentBonus.Count > 0 && modified)
                    ___cacheGetItemHealPercentBonus[0] = __result;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Character), "GetItemHealReceivedFlatBonus")]
        public static void GetItemHealReceivedFlatBonusPre(ref Character __instance, out bool __state, ref bool ___useCache, ref List<int> ___cacheGetItemHealReceivedFlatBonus)
        {
            __state = false;
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;

            if ((bool)MatchManager.Instance && ___useCache && ___cacheGetItemHealReceivedFlatBonus.Count > 0)
                __state = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "GetItemHealReceivedFlatBonus")]
        public static void GetItemHealReceivedFlatBonusPost(ref Character __instance, bool __state, ref int __result, ref bool ___useCache, ref List<int> ___cacheGetItemHealReceivedFlatBonus, ref HeroData ___heroData, ref string ___subclassName)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            if (__state)
                return;

            if (___heroData != null)
            {
                bool modified = false;
                foreach (string blessing in activeBlessings)
                {
                    CardData blessingCard = Globals.Instance.GetCardData(blessing);
                    if (blessingCard != null && blessingCard.Item != null && blessingCard.Item.HealReceivedFlatBonus != 0)
                    {
                        __result += blessingCard.Item.HealReceivedFlatBonus;
                        modified = true;
                    }
                }
                if ((bool)MatchManager.Instance && ___useCache && ___cacheGetItemHealReceivedFlatBonus.Count > 0 && modified)
                    ___cacheGetItemHealReceivedFlatBonus[0] = __result;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Character), "GetItemHealReceivedPercentBonus")]
        public static void GetItemHealReceivedPercentBonusPre(ref Character __instance, out bool __state, ref bool ___useCache, ref List<float> ___cacheGetItemHealReceivedPercentBonus)
        {
            __state = false;
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;

            if ((bool)MatchManager.Instance && ___useCache && ___cacheGetItemHealReceivedPercentBonus.Count > 0)
                __state = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "GetItemHealReceivedPercentBonus")]
        public static void GetItemHealReceivedPercentBonusPost(ref Character __instance, bool __state, ref float __result, ref bool ___useCache, ref List<float> ___cacheGetItemHealReceivedPercentBonus, ref HeroData ___heroData, ref string ___subclassName)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            if (__state)
                return;

            if (___heroData != null)
            {
                bool modified = false;
                foreach (string blessing in activeBlessings)
                {
                    CardData blessingCard = Globals.Instance.GetCardData(blessing);
                    if (blessingCard != null && blessingCard.Item != null && (double)blessingCard.Item.HealReceivedPercentBonus != 0.0)
                    {
                        __result += blessingCard.Item.HealReceivedPercentBonus;
                        modified = true;
                    }
                }
                if ((bool)MatchManager.Instance && ___useCache && ___cacheGetItemHealReceivedPercentBonus.Count > 0 && modified)
                    ___cacheGetItemHealReceivedPercentBonus[0] = __result;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "GetItemHealPercentDictionary")]
        public static void GetItemHealPercentDictionaryPost(ref Character __instance, ref Dictionary<string, int> __result, ref HeroData ___heroData)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;

            if (___heroData != null)
            {
                foreach (string blessing in activeBlessings)
                {
                    CardData blessingCard = Globals.Instance.GetCardData(blessing);
                    if (blessingCard != null && blessingCard.Item != null && (double)blessingCard.Item.HealPercentBonus != 0.0)
                    {
                        StringBuilder stringBuilder = new StringBuilder();
                        stringBuilder.Append(blessingCard.CardName);
                        stringBuilder.Append("_card");
                        __result.Add(stringBuilder.ToString(), (int)blessingCard.Item.HealPercentBonus);
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "GetItemHealFlatDictionary")]
        public static void GetItemHealFlatDictionaryPost(ref Character __instance, ref Dictionary<string, int> __result, ref HeroData ___heroData)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;

            if (___heroData != null)
            {
                foreach (string blessing in activeBlessings)
                {
                    CardData blessingCard = Globals.Instance.GetCardData(blessing);
                    if (blessingCard != null && blessingCard.Item != null && (double)blessingCard.Item.HealFlatBonus != 0.0)
                    {
                        StringBuilder stringBuilder = new StringBuilder();
                        stringBuilder.Append(blessingCard.CardName);
                        stringBuilder.Append("_card");
                        __result.Add(stringBuilder.ToString(), blessingCard.Item.HealFlatBonus);
                    }
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Character), "GetItemAuraCurseModifiers")]
        public static void GetItemAuraCurseModifiersPre(ref Character __instance, out bool __state, ref bool ___useCache, ref Dictionary<string, int> ___cacheGetItemAuraCurseModifiers)
        {
            __state = false;
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;

            if ((bool)MatchManager.Instance && ___useCache && ___cacheGetItemAuraCurseModifiers.Count > 0)
                __state = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "GetItemAuraCurseModifiers")]
        public static void GetItemAuraCurseModifiersPost(ref Character __instance, bool __state, ref Dictionary<string, int> __result, ref bool ___useCache, ref Dictionary<string, int> ___cacheGetItemAuraCurseModifiers, ref HeroData ___heroData, ref string ___subclassName)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            if (__state)
                return;

            if (___heroData != null)
            {
                bool modified = false;
                foreach (string blessing in activeBlessings)
                {
                    CardData blessingCard = Globals.Instance.GetCardData(blessing);
                    if (blessingCard != null && blessingCard.Item != null)
                    {
                        if (blessingCard.Item.AuracurseBonus1 != null)
                        {
                            if (__result.ContainsKey(blessingCard.Item.AuracurseBonus1.Id))
                                __result[blessingCard.Item.AuracurseBonus1.Id] += blessingCard.Item.AuracurseBonusValue1;
                            else
                                __result[blessingCard.Item.AuracurseBonus1.Id] = blessingCard.Item.AuracurseBonusValue1;

                            modified = true;
                        }
                        if (blessingCard.Item.AuracurseBonus2 != null)
                        {
                            if (__result.ContainsKey(blessingCard.Item.AuracurseBonus2.Id))
                                __result[blessingCard.Item.AuracurseBonus2.Id] += blessingCard.Item.AuracurseBonusValue2;
                            else
                                __result[blessingCard.Item.AuracurseBonus2.Id] = blessingCard.Item.AuracurseBonusValue2;

                            modified = true;
                        }
                    }
                }
                if ((bool)MatchManager.Instance && ___useCache && ___cacheGetItemAuraCurseModifiers.Count > 0 && modified)
                    ___cacheGetItemAuraCurseModifiers = __result;
            }
        }



        [HarmonyPrefix]
        [HarmonyPatch(typeof(Character), "GetItemDamageFlatModifiers")]
        public static void GetItemDamageFlatModifiersPre(ref Character __instance, Enums.DamageType DamageType, out bool __state, ref bool ___useCache, ref Dictionary<Enums.DamageType, int> ___cacheGetItemDamageFlatModifiers)
        {
            __state = false;
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;

            if ((bool)MatchManager.Instance && ___useCache && ___cacheGetItemDamageFlatModifiers.ContainsKey(DamageType))
                __state = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "GetItemDamageFlatModifiers")]
        public static void GetItemDamageFlatModifiersPost(ref Character __instance, Enums.DamageType DamageType, bool __state, ref int __result, ref bool ___useCache, ref Dictionary<Enums.DamageType, int> ___cacheGetItemDamageFlatModifiers, ref HeroData ___heroData)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            if (__state)
                return;

            if (___heroData != null)
            {
                bool modified = false;
                foreach (string blessing in activeBlessings)
                {
                    CardData blessingCard = Globals.Instance.GetCardData(blessing);
                    if (blessingCard != null && blessingCard.Item != null)
                    {
                        if (blessingCard.Item.DamageFlatBonus == DamageType || blessingCard.Item.DamageFlatBonus == Enums.DamageType.All)
                        {
                            __result += blessingCard.Item.DamageFlatBonusValue;
                            modified = true;
                        }
                        if (blessingCard.Item.DamageFlatBonus2 == DamageType || blessingCard.Item.DamageFlatBonus2 == Enums.DamageType.All)
                        {
                            __result += blessingCard.Item.DamageFlatBonusValue2;
                            modified = true;
                        }
                        if (blessingCard.Item.DamageFlatBonus3 == DamageType || blessingCard.Item.DamageFlatBonus3 == Enums.DamageType.All)
                        {
                            __result += blessingCard.Item.DamageFlatBonusValue3;
                            modified = true;
                        }
                        modified = true;
                    }
                }
                if ((bool)MatchManager.Instance && ___useCache && ___cacheGetItemDamageFlatModifiers.ContainsKey(DamageType) && modified)
                    ___cacheGetItemDamageFlatModifiers[DamageType] = __result;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Character), "GetItemDamagePercentModifiers")]
        public static void GetItemDamagePercentModifiersPre(ref Character __instance, Enums.DamageType DamageType, out bool __state, ref bool ___useCache, ref Dictionary<Enums.DamageType, float> ___cacheGetItemDamagePercentModifiers)
        {
            __state = false;
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;

            if ((bool)MatchManager.Instance && ___useCache && ___cacheGetItemDamagePercentModifiers.ContainsKey(DamageType))
                __state = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "GetItemDamagePercentModifiers")]
        public static void GetItemDamagePercentModifiersPost(ref Character __instance, Enums.DamageType DamageType, bool __state, ref float __result, ref bool ___useCache, ref Dictionary<Enums.DamageType, float> ___cacheGetItemDamagePercentModifiers, ref HeroData ___heroData)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            if (__state)
                return;

            if (___heroData != null)
            {
                bool modified = false;
                foreach (string blessing in activeBlessings)
                {
                    CardData blessingCard = Globals.Instance.GetCardData(blessing);
                    if (blessingCard != null && blessingCard.Item != null)
                    {
                        if (blessingCard.Item.DamagePercentBonus == DamageType || blessingCard.Item.DamagePercentBonus == Enums.DamageType.All)
                        {
                            __result += blessingCard.Item.DamagePercentBonusValue;
                            modified = true;
                        }
                        if (blessingCard.Item.DamagePercentBonus2 == DamageType || blessingCard.Item.DamagePercentBonus2 == Enums.DamageType.All)
                        {
                            __result += blessingCard.Item.DamagePercentBonusValue2;
                            modified = true;
                        }
                        if (blessingCard.Item.DamagePercentBonus3 == DamageType || blessingCard.Item.DamagePercentBonus3 == Enums.DamageType.All)
                        {
                            __result += blessingCard.Item.DamagePercentBonusValue3;
                            modified = true;
                        }
                        modified = true;
                    }
                }
                if ((bool)MatchManager.Instance && ___useCache && ___cacheGetItemDamagePercentModifiers.ContainsKey(DamageType) && modified)
                    ___cacheGetItemDamagePercentModifiers[DamageType] = __result;
            }
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "GetItemDamageDonePercentDictionary")]
        public static void GetItemDamageDonePercentDictionaryPost(ref Character __instance, Enums.DamageType DamageType, ref Dictionary<string, int> __result, ref HeroData ___heroData)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;

            if (___heroData != null)
            {
                foreach (string blessing in activeBlessings)
                {
                    CardData blessingCard = Globals.Instance.GetCardData(blessing);
                    float num = 0.0f;
                    if (blessingCard != null && blessingCard.Item != null)
                    {
                        if (blessingCard.Item.DamagePercentBonus == DamageType || blessingCard.Item.DamagePercentBonus == Enums.DamageType.All)
                        {
                            num += blessingCard.Item.DamagePercentBonusValue;
                        }
                        if (blessingCard.Item.DamagePercentBonus2 == DamageType || blessingCard.Item.DamagePercentBonus2 == Enums.DamageType.All)
                        {
                            num += blessingCard.Item.DamagePercentBonusValue2;
                        }
                        if (blessingCard.Item.DamagePercentBonus3 == DamageType || blessingCard.Item.DamagePercentBonus3 == Enums.DamageType.All)
                        {
                            num += blessingCard.Item.DamagePercentBonusValue3;
                        }
                        if ((double)num != 0.0)
                        {
                            StringBuilder stringBuilder = new StringBuilder();
                            stringBuilder.Append(blessingCard.CardName);
                            stringBuilder.Append("_card");
                            __result.Add(stringBuilder.ToString(), (int)num);
                        }
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "GetItemDamageDoneDictionary")]
        public static void GetItemDamageDoneDictionaryPost(ref Character __instance, Enums.DamageType DamageType, ref Dictionary<string, int> __result, ref HeroData ___heroData)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;

            if (___heroData != null)
            {
                foreach (string blessing in activeBlessings)
                {
                    CardData blessingCard = Globals.Instance.GetCardData(blessing);
                    int num = 0;
                    if (blessingCard != null && blessingCard.Item != null)
                    {
                        if (blessingCard.Item.DamageFlatBonus == DamageType || blessingCard.Item.DamageFlatBonus == Enums.DamageType.All)
                        {
                            num += blessingCard.Item.DamageFlatBonusValue;
                        }
                        if (blessingCard.Item.DamageFlatBonus2 == DamageType || blessingCard.Item.DamageFlatBonus2 == Enums.DamageType.All)
                        {
                            num += blessingCard.Item.DamageFlatBonusValue2;
                        }
                        if (blessingCard.Item.DamageFlatBonus3 == DamageType || blessingCard.Item.DamageFlatBonus3 == Enums.DamageType.All)
                        {
                            num += blessingCard.Item.DamageFlatBonusValue3;
                        }
                        if (num != 0)
                        {
                            StringBuilder stringBuilder = new StringBuilder();
                            stringBuilder.Append(blessingCard.CardName);
                            stringBuilder.Append("_card");
                            __result.Add(stringBuilder.ToString(), num);
                        }
                    }
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Character), "GetItemResistModifiers")]
        public static void GetItemResistModifiersPre(ref Character __instance, Enums.DamageType type, out bool __state, ref bool ___useCache, ref Dictionary<Enums.DamageType, int> ___cacheGetItemResistModifiers)
        {
            __state = false;
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;

            if ((bool)MatchManager.Instance && ___useCache && ___cacheGetItemResistModifiers.ContainsKey(type))
                __state = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "GetItemResistModifiers")]
        public static void GetItemResistModifiersPost(ref Character __instance, Enums.DamageType type, bool __state, ref int __result, ref bool ___useCache, ref Dictionary<Enums.DamageType, int> ___cacheGetItemResistModifiers, ref HeroData ___heroData)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            if (__state)
                return;

            if (___heroData != null)
            {
                bool modified = false;
                foreach (string blessing in activeBlessings)
                {
                    CardData blessingCard = Globals.Instance.GetCardData(blessing);
                    if (blessingCard != null && blessingCard.Item != null)
                    {
                        if (blessingCard.Item.ResistModified1 == type || blessingCard.Item.ResistModified1 == Enums.DamageType.All)
                        {
                            __result += blessingCard.Item.ResistModifiedValue1;
                            modified = true;
                        }
                        if (blessingCard.Item.ResistModified2 == type || blessingCard.Item.ResistModified2 == Enums.DamageType.All)
                        {
                            __result += blessingCard.Item.ResistModifiedValue2;
                            modified = true;
                        }
                        if (blessingCard.Item.ResistModified3 == type || blessingCard.Item.ResistModified3 == Enums.DamageType.All)
                        {
                            __result += blessingCard.Item.ResistModifiedValue3;
                            modified = true;
                        }
                        modified = true;
                    }
                }
                if ((bool)MatchManager.Instance && ___useCache && ___cacheGetItemResistModifiers.ContainsKey(type) && modified)
                    ___cacheGetItemResistModifiers[type] = __result;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "GetItemResistModifiersDictionary")]
        public static void GetItemResistModifiersDictionaryPost(ref Character __instance, Enums.DamageType type, ref Dictionary<string, int> __result, ref HeroData ___heroData)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;

            if (___heroData != null)
            {
                foreach (string blessing in activeBlessings)
                {
                    CardData blessingCard = Globals.Instance.GetCardData(blessing);
                    int num = 0;
                    if (blessingCard != null && blessingCard.Item != null)
                    {
                        if (blessingCard.Item.ResistModified1 == type || blessingCard.Item.ResistModified1 == Enums.DamageType.All)
                        {
                            num += blessingCard.Item.ResistModifiedValue1;
                        }
                        if (blessingCard.Item.ResistModified2 == type || blessingCard.Item.ResistModified2 == Enums.DamageType.All)
                        {
                            num += blessingCard.Item.ResistModifiedValue2;
                        }
                        if (blessingCard.Item.ResistModified3 == type || blessingCard.Item.ResistModified3 == Enums.DamageType.All)
                        {
                            num += blessingCard.Item.ResistModifiedValue3;
                        }
                        if (num != 0)
                        {
                            StringBuilder stringBuilder = new StringBuilder();
                            stringBuilder.Append(blessingCard.CardName);
                            stringBuilder.Append("_card");
                            __result.Add(stringBuilder.ToString(), num);
                        }
                    }
                }
            }
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(Character), "GetItemStatModifiers")]
        public static void GetItemStatModifiersPre(ref Character __instance, Enums.CharacterStat stat, out bool __state, ref bool ___useCache, ref Dictionary<Enums.CharacterStat, int> ___cacheGetItemStatModifiers)
        {
            __state = false;
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;

            if ((bool)MatchManager.Instance && ___useCache && ___cacheGetItemStatModifiers.ContainsKey(stat))
                __state = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "GetItemStatModifiers")]
        public static void GetItemStatModifiersPost(ref Character __instance, Enums.CharacterStat stat, bool __state, ref int __result, ref bool ___useCache, ref Dictionary<Enums.CharacterStat, int> ___cacheGetItemStatModifiers, ref HeroData ___heroData)
        {
            if (GameManager.Instance.IsObeliskChallenge() || GameManager.Instance.IsWeeklyChallenge())
                return;
            if (__state)
                return;

            if (___heroData != null)
            {
                bool modified = false;
                foreach (string blessing in activeBlessings)
                {
                    CardData blessingCard = Globals.Instance.GetCardData(blessing);
                    if (blessingCard != null && blessingCard.Item != null)
                    {
                        if (blessingCard.Item.CharacterStatModified == stat)
                        {
                            __result += blessingCard.Item.CharacterStatModifiedValue;
                            modified = true;
                        }
                        if (blessingCard.Item.CharacterStatModified2 == stat)
                        {
                            __result += blessingCard.Item.CharacterStatModifiedValue2;
                            modified = true;
                        }
                        if (blessingCard.Item.CharacterStatModified3 == stat)
                        {
                            __result += blessingCard.Item.CharacterStatModifiedValue3;
                            modified = true;
                        }
                        modified = true;
                    }
                }
                if ((bool)MatchManager.Instance && ___useCache && ___cacheGetItemStatModifiers.ContainsKey(stat) && modified)
                    ___cacheGetItemStatModifiers[stat] = __result;
            }
        }

        private static void AddNewBlessings()
        {
            Dictionary<string, CardData> ____CardsSource = Traverse.Create(Globals.Instance).Field<Dictionary<string, CardData>>("_CardsSource").Value;
            Dictionary<string, CardData> ____Cards = Traverse.Create(Globals.Instance).Field<Dictionary<string, CardData>>("_Cards").Value;
            Dictionary<Enums.CardType, List<string>> ____CardItemByType = Traverse.Create(Globals.Instance).Field<Dictionary<Enums.CardType, List<string>>>("_CardItemByType").Value;
            Dictionary<Enums.CardType, List<string>> ____CardListByType = Traverse.Create(Globals.Instance).Field<Dictionary<Enums.CardType, List<string>>>("_CardListByType").Value;
            Dictionary<Enums.CardClass, List<string>> ____CardListByClass = Traverse.Create(Globals.Instance).Field<Dictionary<Enums.CardClass, List<string>>>("_CardListByClass").Value;
            List<string> ____CardListNotUpgraded = Traverse.Create(Globals.Instance).Field<List<string>>("_CardListNotUpgraded").Value;
            Dictionary<Enums.CardClass, List<string>> ____CardListNotUpgradedByClass = Traverse.Create(Globals.Instance).Field<Dictionary<Enums.CardClass, List<string>>>("_CardListNotUpgradedByClass").Value;
            Dictionary<string, List<string>> ____CardListByClassType = Traverse.Create(Globals.Instance).Field<Dictionary<string, List<string>>>("_CardListByClassType").Value;
            Dictionary<string, int> ____CardEnergyCost = Traverse.Create(Globals.Instance).Field<Dictionary<string, int>>("_CardEnergyCost").Value;

            CardData armageddon = GenerateBlessingCard("endlessarmageddon");
            armageddon.Item.Activation = blessingBeginRound;
            armageddon.Item.CardNum = 1;
            armageddon.Item.CardPlace = Enums.CardPlace.Cast;

            ____CardsSource.Add(armageddon.Id.ToLower(), armageddon);
            ____Cards.Add(armageddon.Id.ToLower(), armageddon);
        }

        public static CardData GenerateBlessingCard(string blessingId)
        {
            return UnityEngine.Object.Instantiate(new CardData()
            {
                CardName = "",
                Id = blessingId,
                InternalId = "",
                Visible = true,
                UpgradesTo1 = "",
                UpgradesTo2 = "",
                CardUpgraded = Enums.CardUpgraded.No,
                UpgradedFrom = "",
                BaseCard = "",
                CardNumber = 0,
                Description = "",
                Fluff = "",
                DescriptionNormalized = "",
                KeyNotes = [],
                Sprite = null,
                Sound = null,
                SoundPreAction = null,
                EffectPreAction = "",
                EffectCaster = "",
                EffectPostCastDelay = 0.0f,
                EffectCasterRepeat = false,
                EffectCastCenter = false,
                EffectTrail = "",
                EffectTrailRepeat = false,
                EffectTrailSpeed = 0.0f,
                EffectTrailAngle = Enums.EffectTrailAngle.Parabolic,
                EffectTarget = "",
                MaxInDeck = 0,
                CardRarity = Enums.CardRarity.Common,
                CardType = Enums.CardType.None,
                CardTypeAux = [],
                CardClass = Enums.CardClass.None,
                EnergyCost = 0,
                EnergyCostOriginal = 0,
                EnergyCostForShow = 0,
                Playable = true,
                AutoplayDraw = false,
                AutoplayEndTurn = false,
                TargetType = Enums.CardTargetType.Single,
                TargetSide = Enums.CardTargetSide.Anyone,
                TargetPosition = Enums.CardTargetPosition.Anywhere,
                EffectRequired = "",
                EffectRepeat = 0,
                EffectRepeatDelay = 0.0f,
                EffectRepeatEnergyBonus = 0,
                EffectRepeatMaxBonus = 0,
                EffectRepeatTarget = Enums.EffectRepeatTarget.NoRepeat,
                EffectRepeatModificator = 0,
                DamageType = Enums.DamageType.None,
                Damage = 0,
                DamagePreCalculated = 0,
                DamageSides = 0,
                DamageSidesPreCalculated = 0,
                DamageSelf = 0,
                DamageSelfPreCalculated = 0,
                DamageSelfPreCalculated2 = 0,
                IgnoreBlock = false,
                DamageType2 = Enums.DamageType.None,
                Damage2 = 0,
                DamagePreCalculated2 = 0,
                DamageSides2 = 0,
                DamageSidesPreCalculated2 = 0,
                DamageSelf2 = 0,
                IgnoreBlock2 = false,
                SelfHealthLoss = 0,
                DamageEnergyBonus = 0,
                Heal = 0,
                HealSides = 0,
                HealSelf = 0,
                HealEnergyBonus = 0,
                HealSelfPerDamageDonePercent = 0.0f,
                HealCurses = 0,
                HealAuraCurseSelf = null,
                HealAuraCurseName = null,
                HealAuraCurseName2 = null,
                HealAuraCurseName3 = null,
                HealAuraCurseName4 = null,
                DispelAuras = 0,
                EnergyRecharge = 0,
                Aura = null,
                AuraSelf = null,
                AuraCharges = 0,
                Aura2 = null,
                AuraSelf2 = null,
                AuraCharges2 = 0,
                Aura3 = null,
                AuraSelf3 = null,
                AuraCharges3 = 0,
                Curse = null,
                CurseSelf = null,
                CurseCharges = 0,
                Curse2 = null,
                CurseSelf2 = null,
                CurseCharges2 = 0,
                Curse3 = null,
                CurseSelf3 = null,
                CurseCharges3 = 0,
                PushTarget = 0,
                PullTarget = 0,
                DrawCard = 0,
                DiscardCard = 0,
                DiscardCardType = Enums.CardType.None,
                DiscardCardTypeAux = [],
                DiscardCardAutomatic = false,
                DiscardCardPlace = Enums.CardPlace.Discard,
                AddCard = 0,
                AddCardId = "",
                AddCardType = Enums.CardType.None,
                AddCardTypeAux = [],
                AddCardChoose = 0,
                AddCardFrom = Enums.CardFrom.Game,
                AddCardPlace = Enums.CardPlace.Discard,
                AddCardReducedCost = 0,
                AddCardCostTurn = false,
                AddCardVanish = false,
                LookCards = 0,
                LookCardsDiscardUpTo = 0,
                SummonUnit = null,
                SummonUnitNum = 0,
                Vanish = true,
                Lazy = false,
                Innate = false,
                Corrupted = false,
                EndTurn = false,
                MoveToCenter = false,
                ModifiedByTrait = false,
                EffectPostTargetDelay = 0.0f,
                SpecialValueGlobal = Enums.CardSpecialValue.None,
                SpecialValueModifierGlobal = 0.0f,
                SpecialValue1 = Enums.CardSpecialValue.None,
                SpecialValueModifier1 = 0.0f,
                SpecialValue2 = Enums.CardSpecialValue.None,
                SpecialValueModifier2 = 0.0f,
                DamageSpecialValueGlobal = false,
                DamageSpecialValue1 = false,
                DamageSpecialValue2 = false,
                Damage2SpecialValueGlobal = false,
                Damage2SpecialValue1 = false,
                Damage2SpecialValue2 = false,
                SpecialAuraCurseNameGlobal = null,
                SpecialAuraCurseName1 = null,
                SpecialAuraCurseName2 = null,
                AuraChargesSpecialValue1 = false,
                AuraChargesSpecialValue2 = false,
                AuraChargesSpecialValueGlobal = false,
                AuraCharges2SpecialValue1 = false,
                AuraCharges2SpecialValue2 = false,
                AuraCharges2SpecialValueGlobal = false,
                AuraCharges3SpecialValue1 = false,
                AuraCharges3SpecialValue2 = false,
                AuraCharges3SpecialValueGlobal = false,
                CurseChargesSpecialValue1 = false,
                CurseChargesSpecialValue2 = false,
                CurseChargesSpecialValueGlobal = false,
                CurseCharges2SpecialValue1 = false,
                CurseCharges2SpecialValue2 = false,
                CurseCharges2SpecialValueGlobal = false,
                CurseCharges3SpecialValue1 = false,
                CurseCharges3SpecialValue2 = false,
                CurseCharges3SpecialValueGlobal = false,
                HealSpecialValueGlobal = false,
                HealSpecialValue1 = false,
                HealSpecialValue2 = false,
                SelfHealthLossSpecialGlobal = false,
                SelfHealthLossSpecialValue1 = false,
                SelfHealthLossSpecialValue2 = false,
                FluffPercent = 0.0f,
                Item = GenerateCardItem(blessingId),
                SummonAura = null,
                SummonAuraCharges = 0,
                SummonAura2 = null,
                SummonAuraCharges2 = 0,
                SummonAura3 = null,
                SummonAuraCharges3 = 0,
                HealSelfSpecialValueGlobal = false,
                HealSelfSpecialValue1 = false,
                HealSelfSpecialValue2 = false,
                PetModel = null,
                PetFront = false,
                PetOffset = Vector2.zero,
                PetSize = Vector2.zero,
                PetInvert = false,
                IsPetAttack = false,
                IsPetCast = false,
                UpgradesToRare = null,
                ExhaustCounter = 0,
                Starter = false,
                Target = "",
                ItemEnchantment = null,
                AddCardList = [],
                ShowInTome = false,
                LookCardsVanishUpTo = 0,
                TransferCurses = 0,
                KillPet = false,
                ReduceCurses = 0,
                AcEnergyBonus = null,
                AcEnergyBonusQuantity = 0,
                EnergyReductionPermanent = 0,
                EnergyReductionTemporal = 0,
                EnergyReductionToZeroPermanent = false,
                EnergyReductionToZeroTemporal = false,
                AcEnergyBonus2 = null,
                AcEnergyBonus2Quantity = 0,
                StealAuras = 0,
                FlipSprite = false,
                SoundPreActionFemale = null,
                ReduceAuras = 0,
                IncreaseCurses = 0,
                IncreaseAuras = 0,
                OnlyInWeekly = false,
                RelatedCard = "",
                RelatedCard2 = "",
                RelatedCard3 = "",
                GoldGainQuantity = 0,
                ShardsGainQuantity = 0,
                Sku = "",
                EnergyRechargeSpecialValueGlobal = false,
                DrawCardSpecialValueGlobal = false,
                SelfKillHiddenSeconds = 0.0f,
                SoundHitReworkDelay = 0.0f,
                Evolve = false,
                Metamorph = false
            });
        }

        public static ItemData GenerateCardItem(string itemId)
        {
            return UnityEngine.Object.Instantiate(new ItemData()
            {
                Acg1MultiplyByEnergyUsed = false,
                Acg2MultiplyByEnergyUsed = false,
                Acg3MultiplyByEnergyUsed = false,
                Activation = Enums.EventActivation.None,
                ActivationOnlyOnHeroes = false,
                AuracurseBonus1 = null,
                AuracurseBonus2 = null,
                AuracurseBonusValue1 = 0,
                AuracurseBonusValue2 = 0,
                AuracurseCustomAC = null,
                AuracurseCustomModValue1 = 0,
                AuracurseCustomModValue2 = 0,
                AuracurseCustomString = null,
                AuracurseGain1 = null,
                AuracurseGain2 = null,
                AuracurseGain3 = null,
                AuracurseGainValue1 = 0,
                AuracurseGainValue2 = 0,
                AuracurseGainValue3 = 0,
                AuracurseGainSelf1 = null,
                AuracurseGainSelf2 = null,
                AuracurseGainSelfValue1 = 0,
                AuracurseGainSelfValue2 = 0,
                AuracurseImmune1 = null,
                AuracurseImmune2 = null,
                AuraCurseNumForOneEvent = 0,
                AuraCurseSetted = null,
                CardNum = 0,
                CardPlace = Enums.CardPlace.Discard,
                CardsReduced = 0,
                CardToGain = null,
                CardToGainList = new List<CardData>(),
                CardToGainType = Enums.CardType.None,
                CardToReduceType = Enums.CardType.None,
                CastedCardType = Enums.CardType.None,
                CastEnchantmentOnFinishSelfCast = false,
                ChanceToDispel = 0,
                ChanceToDispelNum = 0,
                CharacterStatModified = Enums.CharacterStat.None,
                CharacterStatModified2 = Enums.CharacterStat.None,
                CharacterStatModified3 = Enums.CharacterStat.None,
                CharacterStatModifiedValue = 0,
                CharacterStatModifiedValue2 = 0,
                CharacterStatModifiedValue3 = 0,
                CostReducePermanent = false,
                CostReduceReduction = 0,
                CostReduceEnergyRequirement = 0,
                CostReduction = 0,
                CostZero = false,
                CursedItem = false,
                DamageFlatBonus = Enums.DamageType.None,
                DamageFlatBonus2 = Enums.DamageType.None,
                DamageFlatBonus3 = Enums.DamageType.None,
                DamageFlatBonusValue = 0,
                DamageFlatBonusValue2 = 0,
                DamageFlatBonusValue3 = 0,
                DamagePercentBonus = Enums.DamageType.None,
                DamagePercentBonus2 = Enums.DamageType.None,
                DamagePercentBonus3 = Enums.DamageType.None,
                DamagePercentBonusValue = 0.0f,
                DamagePercentBonusValue2 = 0.0f,
                DamagePercentBonusValue3 = 0.0f,
                DestroyAfterUse = false,
                DestroyAfterUses = 0,
                DestroyEndOfTurn = false,
                DestroyStartOfTurn = false,
                DrawCards = 0,
                DrawMultiplyByEnergyUsed = false,
                DropOnly = true,
                DttMultiplyByEnergyUsed = false,
                DuplicateActive = false,
                EffectCaster = null,
                EffectItemOwner = null,
                EffectTarget = "",
                EffectCasterDelay = 0.0f,
                EffectTargetDelay = 0.0f,
                EmptyHand = false,
                EnergyQuantity = 0,
                ExactRound = 0,
                HealFlatBonus = 0,
                HealPercentBonus = 0.0f,
                HealPercentQuantity = 0,
                HealPercentQuantitySelf = 0,
                HealQuantity = 0,
                HealReceivedFlatBonus = 0,
                HealReceivedPercentBonus = 0.0f,
                Id = itemId,
                IsEnchantment = false,
                ItemSound = null,
                ItemTarget = Enums.ItemTarget.Self,
                LowerOrEqualPercentHP = 100.0f,
                MaxHealth = 0,
                ModifiedDamageType = Enums.DamageType.None,
                NotShowCharacterBonus = false,
                OnlyAddItemToNPCs = false,
                PassSingleAndCharacterRolls = false,
                PercentDiscountShop = 0,
                PercentRetentionEndGame = 0,
                Permanent = false,
                QuestItem = false,
                ReduceHighestCost = false,
                ResistModified1 = Enums.DamageType.None,
                ResistModified2 = Enums.DamageType.None,
                ResistModified3 = Enums.DamageType.None,
                ResistModifiedValue1 = 0,
                ResistModifiedValue2 = 0,
                ResistModifiedValue3 = 0,
                RoundCycle = 0,
                SpriteBossDrop = null,
                TimesPerCombat = 0,
                TimesPerTurn = 0,
                UsedEnergy = false,
                UseTheNextInsteadWhenYouPlay = false,
                Vanish = true
            });
        }
    }
}
﻿using Aki.Reflection.Patching;
using Comfort.Common;
using EFT;
using EFT.Communications;
using Newtonsoft.Json;
using SAIN.Components;
using SAIN;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;

namespace RAID_REVIEW
{
    public class RAID_REVIEW_Player_OnGameStartedPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GameWorld).GetMethod("OnGameStarted", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        [PatchPostfix]
        private static void PatchPostFix(ref GameWorld __instance)
        {
            try {
                if (__instance.LocationId != "hideout")
                {
                    Logger.LogInfo("RAID_REVIEW :::: INFO :::: RAID Settings Loaded");

                    if (!RAID_REVIEW.MODS_SEARCHED)
                    {
                        Logger.LogInfo("RAID_REVIEW :::: INFO :::: Searching For Supported Mods");
                        RAID_REVIEW.MODS_SEARCHED = true;
                        if (RAID_REVIEW.DetectMod("me.sol.sain"))
                        {
                            Logger.LogInfo("RAID_REVIEW :::: INFO :::: Found 'SAIN' Enabling Plugin Features for SAIN.");
                            RAID_REVIEW.SOLARINT_SAIN__DETECTED = true;
                            RAID_REVIEW.RAID_REVIEW__DETECTED_MODS.Add("SAIN");
                        }
                        Logger.LogInfo("RAID_REVIEW :::: INFO :::: Finished Searching For Supported Mods");
                    }
                    Logger.LogInfo("RAID_REVIEW :::: INFO :::: RAID Settings Loaded");

                    RAID_REVIEW.inRaid = true;
                    RAID_REVIEW.stopwatch.Reset();
                    RAID_REVIEW.stopwatch.Start();

                    RAID_REVIEW.trackingRaid = new TrackingRaid
                    {
                        id = Guid.NewGuid().ToString("D"),
                        profileId = RAID_REVIEW.myPlayer.ProfileId,
                        time = DateTime.Now,
                        detectedMods = RAID_REVIEW.RAID_REVIEW__DETECTED_MODS.Count > 0 ? string.Join(",", RAID_REVIEW.RAID_REVIEW__DETECTED_MODS) : "",
                        location = RAID_REVIEW.gameWorld.LocationId,
                        timeInRaid = RAID_REVIEW.stopwatch.IsRunning ? RAID_REVIEW.stopwatch.ElapsedMilliseconds : 0
                    };

                    Telemetry.Send("START", JsonConvert.SerializeObject(RAID_REVIEW.trackingRaid));

                    var newTrackingPlayer = new TrackingPlayer
                    {
                        profileId = RAID_REVIEW.myPlayer.ProfileId,
                        name = RAID_REVIEW.myPlayer.Profile.Nickname,
                        level = RAID_REVIEW.myPlayer.Profile.Info.Level,
                        team = RAID_REVIEW.myPlayer.Side,
                        group = 0,
                        spawnTime = RAID_REVIEW.stopwatch.ElapsedMilliseconds,
                        type = "HUMAN",
                        mod_SAIN_brain = "PLAYER"
                    };

                    RAID_REVIEW.trackingPlayers[newTrackingPlayer.profileId] = newTrackingPlayer;
                    Telemetry.Send("PLAYER", JsonConvert.SerializeObject(newTrackingPlayer));

                    RAID_REVIEW.inRaid = true;
                    Logger.LogInfo("RAID_REVIEW :::: INFO :::: RAID Information Populated");

                    NotificationManagerClass.DisplayMessageNotification("Raid Review Recording Started", ENotificationDurationType.Long);

                    if(RAID_REVIEW.SOLARINT_SAIN__DETECTED)
                    {
                        RAID_REVIEW.searchingForSainComponents = true;
                        _ = CheckForSainComponents();
                    }

                    return;
                }
                return;
            }

            catch (Exception ex)
            {
                Logger.LogError($"{ex.Message}");
            }
        }

        public static async Task CheckForSainComponents()
        {
            while (RAID_REVIEW.searchingForSainComponents)
            {
                await Task.Delay(10000);
                if (RAID_REVIEW.sainBotController == null)
                {
                    Logger.LogInfo("RAID_REVIEW :::: INFO :::: Looking For SAIN Bot Controller");
                    if (RAID_REVIEW.gameWorld != null)
                    {
                        RAID_REVIEW.sainBotController = RAID_REVIEW.gameWorld.GetComponent<SAINBotController>();
                        if(RAID_REVIEW.sainBotController != null)
                            Logger.LogInfo("RAID_REVIEW :::: INFO :::: SAIN Bot Controller Found");
                        else
                            Logger.LogInfo("RAID_REVIEW :::: INFO :::: SAIN Bot Controller Not Found");
                    }
                    else
                    {
                        Logger.LogInfo("RAID_REVIEW :::: INFO :::: GameWorld Not Found");
                    }
                }
                else
                {
                    foreach (var botComponent in RAID_REVIEW.sainBotController.Bots.Values)
                    {
                        var profileId = botComponent.Player.ProfileId;
                        if (!RAID_REVIEW.updatedBots.ContainsKey(profileId) && RAID_REVIEW.trackingPlayers.ContainsKey(profileId))
                        {
                            var trackingPlayer = RAID_REVIEW.trackingPlayers[profileId];
                            trackingPlayer.mod_SAIN_brain = Enum.GetName(typeof(EPersonality), botComponent.Info.Personality);
                            if (!botComponent.Info.Profile.IsPMC)
                            {
                                trackingPlayer.type = BotHelper.getBotType(botComponent);
                            }
                            RAID_REVIEW.trackingPlayers[trackingPlayer.profileId] = trackingPlayer;
                            RAID_REVIEW.updatedBots[trackingPlayer.profileId] = trackingPlayer;
                            Logger.LogInfo($"RAID_REVIEW :::: INFO :::: Updating player {trackingPlayer.name} with brain {trackingPlayer.mod_SAIN_brain} and type {trackingPlayer.type}");
                            _ = Telemetry.Send("PLAYER_UPDATE", JsonConvert.SerializeObject(trackingPlayer));
                        }
                    }
                }
            }
            return;
        }
    }
}

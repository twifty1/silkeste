using System;

namespace MadelineInPharloom.Sheets;

public static class Visuals
{
	public const float PxPerUnit = 8f;

	public const float SpriteScale = 0.9f;

	public const float HairCount = 4f;

	public const string HairColorFlash = "FFFFFF";

	public const float HairFlashTime = 0.12f;

	public const float HairStepPerSegment = 2f;

	public const float HairStepInFacing = 0.5f;

	public const float HairStepApproach = 64f;

	public const float HairMaxLink = 3f;

	public const float HairHeadOffset = -9f;

	public const float SpriteOriginX = 16f;

	public const float SpriteOriginY = 32f;

	public const string TiredFlashColor = "FF0000";

	public const float TiredFlashInterval = 0.05f;

	public const float BenchSeatHeight = 0.85f;

	public const float DeathBounce = 24f;

	public const float DeathBounceTime = 0.5f;

	public const float DeathEffectDelay = 0.375f;

	public const float DeathEffectDuration = 0.834f;

	public const float DeathEffectRadius = 24f;

	public const float WipeStart = 0.917f;

	public const float WipeDuration = 0.5f;

	public const float WipeRows = 6f;

	public const float WipeAngle = 64f;

	public const float WipeRevealDelay = 0.25f;

	public const float SilksongFadeAt = 0.5f;

	public const float HazardWipeStart = 0.375f;

	public const float HazardReloadWait = 0.3f;

	public const float WallOffsetPx = 2f;

	public const float WallOffsetSpeed = 16f;

	public const float LayerDepthStep = 0.0004f;

	public const float DashTrailAlpha = 0.6f;

	public const float DashFreezeFrames = 3f;

	public const float DashFreezeMinTimescale = 0.25f;

	public const float DashShakeTime = 0.2f;

	public const float DashShakeInterval = 0.04f;

	public const float DashShakePxPerSecond = 10f;

	public const float DashFxCenterPx = 5.5f;

	public const float SlashFrameTime = 0.1f;

	public const float SlashDrift = 8f;

	public const float DustSpeedMin = 5f;

	public const float DustSpeedMax = 15f;

	public const float DustLifeMin = 0.3f;

	public const float DustLifeMax = 0.5f;

	public const float DustSize = 0.7f;

	public const float DustSizeRange = 0.2f;

	public const float DustDirectionRange = 0.5f;

	public const float DustGravity = 4f;

	public const float DustBurstRange = 4f;

	public const float DustJumpCount = 4f;

	public const float DustLandCount = 8f;

	public const float DustLandMinSpeed = 80f;

	public const float DustWallJumpOffset = 2f;

	public const float WallslideDustFraction = 0.65f;

	public const float WallslideDustOffset = 5f;

	public const float WallslideDustInterval = 0.01f;

	public const string FootstepFrames = "runSlow:0,6;runFast:0,6;push:8,15;flip:4";

	public const string HandholdFrames = "climbup:5";

	public const float DeathsShowTime = 2.5f;

	public const float DeathsFadeTime = 0.25f;

	public const float DeathsMargin = 48f;

	public const float DeathsIconWidth = 60f;

	public const float DeathsStroke = 2f;

	public const float DeathsWiggleTime = 0.5f;

	public const float DeathsWiggleFrequency = 3f;

	public const float DeathsWigglePx = 18f;

	public const string DeathsFlashColor = "FF5E76";

	public const float DeathsFlashTime = 0.5f;

	public const float DeathsFlashInterval = 0.05f;

	public const float IdleDelay = 3f;

	public const float IdleChance = 0.2f;

	public const float WakeSoundFrame = 19f;

	public const float RespawnTime = 0.6f;

	public const float RespawnSquashX = 1.5f;

	public const float RespawnSquashY = 0.5f;

	public const float WakeStandFromFrame = 15f;

	public const string StarFlyColor = "FFD65C";

	public const string FeatherColor = "F2EB6D";

	public const string FeatherColor2 = "FFF20F";

	public const float FeatherCount = 10f;

	public const float FeatherRange = 6f;

	public const float FeatherSpeedMin = 60f;

	public const float FeatherSpeedMax = 70f;

	public const float FeatherSpeedMultiplier = 0.1f;

	public const float FeatherLifeMin = 0.6f;

	public const float FeatherLifeMax = 0.9f;

	public const float FeatherSpinMin = MathF.PI / 6f;

	public const float FeatherSpinMax = 1.3962634f;

	public const float FastFallStretchFrom = 0.5f;

	public const float FastFallStretchX = 0.5f;

	public const float FastFallStretchY = 1.5f;

	public const float PostcardInTime = 1.25f;

	public const float PostcardOutTime = 1f;

	public const float PostcardRise = 200f;

	public const float PostcardTurnFrom = -0.1f;

	public const float PostcardTurnTo = 0.05f;

	public const float PostcardTurnOut = 0.1f;

	public const float PostcardTextScale = 0.7f;

	public const float PostcardTextMargin = 120f;

	public const float PostcardTextOffsetY = 40f;

	public const float PostcardTextAlpha = 0.6f;

	public const float PostcardTitleX = 115f;

	public const float PostcardTitleY = 30f;

	public const float PostcardTitleScale = 0.9f;

	public const float PostcardTitleAlpha = 0.7f;

	public const float PostcardButtonDelay = 0.75f;

	public const float PostcardShowDelay = 0.5f;

	public const float PostcardLeftAligned = 1f;

	public const float GliderCarryY = -12f;

	public const float GliderJustifyY = 0.58f;

	public const float GliderTiltAir = MathF.PI / 3f;

	public const float GliderTiltSpeed = 300f;

	public const float GliderTiltRate = MathF.PI;

	public const float GliderOpenSquashX = 1.5f;

	public const float GliderOpenSquashY = 0.6f;

	public const float GliderScaleReturn = 2f;

	public const float GliderFrameTime = 0.06f;

	public const float AssistHoldTime = 1f;

	public const float AssistFadeTime = 0.25f;

	public const float AssistDashFreezeMax = 5f;

	public const float AssistDim = 0.75f;

	public const string MenuHighlightA = "84FF54";

	public const string MenuHighlightB = "FCFF59";

	public const float MemoryCrystalLife = 2.5f;

	public const string MemoryCrystalColor = "9FE8FFD8";

	public const string HairColorNames = "Red:AC3232;Purple:9B3FB5;Ghost Blue:9FC0FF;Pale Blue:D8E6FF;Blue:44B7FF;Pink:FF6DEF;PICO Red:FF004D;PICO Blue:29ADFF;PICO Green:00E436;Snow:D3DDF2;Orange:E07A2E;Violet:8E5BFF;Rust:C04A3C;Ivory:D9D1C5;Light Pink:FFB3D4;Gold:F7C948;Blonde:F2D16B;Brown:7A4A2A;Black:2E2E38;White:F4F4F4;Silver:B8C0CC;Green:4CAF50;Mint:7FE0B5;Teal:2BB3B1;Navy:2C3E8C;Lavender:C3A6FF;Crimson:9E1030;Peach:FFB38A";

	public const string LookDefaultSkins = "Madeline,Badeline,Pink Cat,Sunset";

	public const float AreaCardTime = 4f;

	public const float HornetChinDrop = 8f;

	public const float LookPreviewScale = 7f;

	public const float KnightBenchHeadHeight = 12f;

	public const float AssistTextScale = 0.6f;

	public const float AssistColumnWidth = 900f;

	public const float AssistArrowDistance = 110f;

	public const float AssistArrowSize = 72f;

	public const float BossFightForgetTime = 120f;

	public const float BossCardDelay = 1f;

	public const float BossCardInTime = 0.6f;

	public const float BossCardOutTime = 0.4f;

	public const float BossCardDim = 0.45f;

	public const float BossCardScale = 0.72f;

	public const float BossCardTop = 260f;

	public const string BossCardTitleColor = "8C1C2E";

	public const string BossCardGoldColor = "D8A520";

	public const float BossCardTitleScale = 1.1f;

	public const float BossCardTextScale = 0.8f;

	public const float TimerTop = 225f;

	public const float TimerScale = 0.6f;

	public const float PicoScale = 2f;

	public const float PostcardTextMaxHeight = 400f;

	public const string BossCardTitleAccent = "C8465A";

	public const string BossCardGoldAccent = "F7D769";

	public const string BossCardStampColor = "B8323C";

	public const float GoldenFollowX = 14f;

	public const float GoldenFollowY = 22f;

	public const float GoldenFollowEase = 6f;

	public const float GoldenLaughTime = 0.35f;

	public const float GoldenFlyAccel = 500f;

	public const float GoldenIntroTime = 2.5f;

	public const float GoldenHudSize = 64f;

	public const float GoldenHudTop = 150f;

	public const float TimerSaveSlots = 4f;

	public const string NoBackpackAliases = "sleep=sitDown:last;asleep=sitDown:last;halfWakeUp=sitDown:last;wakeUp=sitDown:reverse;tired=idle;tiredStill=idle;fall=fallSlow;bigFall=fallSlow;bigFallRecover=duck;fallPose=fallSlow;faint=duck:last;fainted=duck:last;hug=idle;slide=duck;edgeBack=edge";
}

using GlobalEnums;
using UnityEngine;

namespace MadelineInPharloom;

public class CelesteWipe : MonoBehaviour
{
	private enum Phase
	{
		Idle,
		Waiting,
		Covering,
		Covered,
		Revealing
	}

	private Phase phase;

	private float clock;

	private float percent;

	private float aliveFor;

	private bool hazard;

	private Material mat;

	public static bool ScreenCovered { get; private set; }

	public static float FadeStart => 1.167f;

	public static float HazardFadeStart => 0.625f;

	private void Update()
	{
		HeroController silentInstance = HeroController.UnsafeInstance;
		GameManager instance = GameManager.instance;
		bool flag = silentInstance != null && (silentInstance.cState.dead || silentInstance.cState.hazardDeath);
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		switch (phase)
		{
		case Phase.Idle:
			if (flag && Plugin.Ready)
			{
				phase = Phase.Waiting;
				clock = 0f;
				hazard = silentInstance.cState.hazardDeath && !silentInstance.cState.dead;
			}
			break;
		case Phase.Waiting:
			clock += unscaledDeltaTime;
			if (clock >= (hazard ? 0.375f : 0.917f))
			{
				phase = Phase.Covering;
				percent = 0f;
			}
			break;
		case Phase.Covering:
			percent = Mathf.MoveTowards(percent, 1f, unscaledDeltaTime / 0.5f);
			if (percent >= 1f)
			{
				phase = Phase.Covered;
				aliveFor = 0f;
			}
			break;
		case Phase.Covered:
		{
			bool flag2 = silentInstance != null && !flag && instance != null && instance.GameState == GameState.PLAYING && !silentInstance.cState.transitioning;
			aliveFor = (flag2 ? (aliveFor + unscaledDeltaTime) : 0f);
			if (aliveFor >= 0.25f)
			{
				phase = Phase.Revealing;
				percent = 0f;
			}
			break;
		}
		case Phase.Revealing:
			percent = Mathf.MoveTowards(percent, 1f, unscaledDeltaTime / 0.5f);
			if (percent >= 1f)
			{
				phase = Phase.Idle;
			}
			break;
		}
		ScreenCovered = phase == Phase.Covering || phase == Phase.Covered;
	}

	private void OnGUI()
	{
		if (Event.current.type != EventType.Repaint || (phase != Phase.Covering && phase != Phase.Covered && phase != Phase.Revealing))
		{
			return;
		}
		if (mat == null)
		{
			Shader shader = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Sprites/Default");
			mat = new Material(shader);
			mat.SetInt("_ZTest", 8);
			mat.SetInt("_Cull", 0);
		}
		bool flag = phase == Phase.Revealing;
		float num = ((phase == Phase.Covered) ? 1f : percent);
		float num2 = (float)Screen.width / 1920f;
		float num3 = (float)Screen.height / 1080f;
		int num4 = 6;
		float num5 = 1100f / (float)num4;
		float num6 = 64f;
		mat.SetPass(0);
		GL.PushMatrix();
		GL.LoadPixelMatrix();
		GL.Begin(4);
		GL.Color(Color.black);
		for (int i = 0; i < num4; i++)
		{
			float num7 = 0f - num6;
			float num8 = -10f + (float)i * num5;
			float num9 = (float)i / (float)num4;
			float num10 = (flag ? (1f - num9) : num9) * 0.3f;
			float num11 = ((num > num10) ? Mathf.Min(1f, (num - num10) / 0.7f) : 0f);
			if (flag)
			{
				num11 = 1f - num11;
			}
			float num12 = (1920f + num6) * num11;
			Vector2[] array = new Vector2[6]
			{
				new Vector2(num7, num8),
				new Vector2(num7 + num12, num8),
				new Vector2(num7, num8 + num5),
				new Vector2(num7 + num12, num8),
				new Vector2(num7 + num12 + num6, num8 + num5),
				new Vector2(num7, num8 + num5)
			};
			for (int j = 0; j < array.Length; j++)
			{
				Vector2 vector = array[j];
				float num13 = (flag ? (1920f - vector.x) : vector.x);
				float num14 = (flag ? (1080f - vector.y) : vector.y);
				GL.Vertex3(num13 * num2, (float)Screen.height - num14 * num3, 0f);
			}
		}
		GL.End();
		GL.PopMatrix();
	}
}

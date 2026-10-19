using System.Collections.Generic;

namespace MadelineInPharloom;

public class CelesteAnim
{
	public string Id;

	public float Delay;

	public bool Loop;

	public string Goto;

	public List<CelesteFrame> Frames = new List<CelesteFrame>();
}

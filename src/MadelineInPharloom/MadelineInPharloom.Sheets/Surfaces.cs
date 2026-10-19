namespace MadelineInPharloom.Sheets;

public static class Surfaces
{
	public static readonly SurfaceRow Dust = new SurfaceRow
	{
		Id = "dust",
		Environment = "Dust",
		CelesteName = "Dirt",
		CelesteSurface = 3
	};

	public static readonly SurfaceRow Grass = new SurfaceRow
	{
		Id = "grass",
		Environment = "Grass",
		CelesteName = "Grass",
		CelesteSurface = 33
	};

	public static readonly SurfaceRow Bone = new SurfaceRow
	{
		Id = "bone",
		Environment = "Bone",
		CelesteName = "Brick",
		CelesteSurface = 8
	};

	public static readonly SurfaceRow Shallowwater = new SurfaceRow
	{
		Id = "shallowwater",
		Environment = "ShallowWater",
		CelesteName = "Dirt",
		CelesteSurface = 3
	};

	public static readonly SurfaceRow Metal = new SurfaceRow
	{
		Id = "metal",
		Environment = "Metal",
		CelesteName = "Girder",
		CelesteSurface = 7
	};

	public static readonly SurfaceRow Noeffect = new SurfaceRow
	{
		Id = "noeffect",
		Environment = "NoEffect",
		CelesteName = "Brick",
		CelesteSurface = 8
	};

	public static readonly SurfaceRow Moss = new SurfaceRow
	{
		Id = "moss",
		Environment = "Moss",
		CelesteName = "CliffsideGrass",
		CelesteSurface = 25
	};

	public static readonly SurfaceRow Sand = new SurfaceRow
	{
		Id = "sand",
		Environment = "Sand",
		CelesteName = "Snow",
		CelesteSurface = 4
	};

	public static readonly SurfaceRow Bell = new SurfaceRow
	{
		Id = "bell",
		Environment = "Bell",
		CelesteName = "Girder",
		CelesteSurface = 7
	};

	public static readonly SurfaceRow Wetmetal = new SurfaceRow
	{
		Id = "wetmetal",
		Environment = "WetMetal",
		CelesteName = "Girder",
		CelesteSurface = 7
	};

	public static readonly SurfaceRow Thinmetal = new SurfaceRow
	{
		Id = "thinmetal",
		Environment = "ThinMetal",
		CelesteName = "Girder",
		CelesteSurface = 7
	};

	public static readonly SurfaceRow Wood = new SurfaceRow
	{
		Id = "wood",
		Environment = "Wood",
		CelesteName = "Wood",
		CelesteSurface = 5
	};

	public static readonly SurfaceRow Silk = new SurfaceRow
	{
		Id = "silk",
		Environment = "Silk",
		CelesteName = "ResortLinens",
		CelesteSurface = 17
	};

	public static readonly SurfaceRow Wetwood = new SurfaceRow
	{
		Id = "wetwood",
		Environment = "WetWood",
		CelesteName = "Wood",
		CelesteSurface = 5
	};

	public static readonly SurfaceRow Runningwater = new SurfaceRow
	{
		Id = "runningwater",
		Environment = "RunningWater",
		CelesteName = "Dirt",
		CelesteSurface = 3
	};

	public static readonly SurfaceRow Peakpuff = new SurfaceRow
	{
		Id = "peakpuff",
		Environment = "PeakPuff",
		CelesteName = "Snow",
		CelesteSurface = 4
	};

	public static readonly SurfaceRow Flowerfield = new SurfaceRow
	{
		Id = "flowerfield",
		Environment = "FlowerField",
		CelesteName = "Grass",
		CelesteSurface = 33
	};

	public static readonly SurfaceRow[] All = new SurfaceRow[17]
	{
		Dust, Grass, Bone, Shallowwater, Metal, Noeffect, Moss, Sand, Bell, Wetmetal,
		Thinmetal, Wood, Silk, Wetwood, Runningwater, Peakpuff, Flowerfield
	};
}

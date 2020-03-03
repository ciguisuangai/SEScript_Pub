//坡度侦测	TerrainAngle.cs by ciguisuangai
//20-02-17

const string TerrCockpitName = "Cockpit";	//驾驶舱名称，用于获取重力信息
const string TerrainCamName = "TerrainCam";	//摄像机名称，安装方向：上方朝前，前方朝下
const float TerrScanDist = 40f;	//地形侦测距离（米）
const float TerrScanSize = 20f;	//TerrScanDist处的侦测范围（米）
//(TerrScanDist > TerrScanSize)

IMyShipController TerrCockpit;
IMyCameraBlock TerrainCam;
Vector3D[] VecCache = new Vector3D[4];
double TerrPitch = 0;
double TerrRoll = 0;
double TerrLeng = 0;
double TerrMyPitch, TerrMyRoll;
Vector3D TerrScanVec;
int TerrTick = 0;

public Program() {
	Runtime.UpdateFrequency = UpdateFrequency.Update1;
	GetTerrainCamera();
}

void GetTerrainCamera() {
	TerrainCam = GridTerminalSystem.GetBlockWithName(TerrainCamName) as IMyCameraBlock;	
	TerrCockpit = GridTerminalSystem.GetBlockWithName(TerrCockpitName) as IMyShipController;	
}

void ScanTerrain() {
	if (TerrainCam != null && TerrCockpit != null) {
		TerrainCam.EnableRaycast = true;
		
		MatrixD RefLookAtMatrix = MatrixD.CreateLookAt(Vector3D.Zero, TerrCockpit.WorldMatrix.Forward, TerrCockpit.WorldMatrix.Up);
		Vector3D MeToGravity = Vector3D.Normalize(Vector3D.TransformNormal(TerrCockpit.GetNaturalGravity(), RefLookAtMatrix));
		TerrMyPitch = Math.Atan2(MeToGravity.Z, -MeToGravity.Y);
		TerrMyRoll = Math.Atan2(MeToGravity.X, -MeToGravity.Y);
		
			
			MatrixD RefLookAtMatrixCam = MatrixD.CreateLookAt(Vector3D.Zero, TerrainCam.WorldMatrix.Forward, TerrainCam.WorldMatrix.Up);
			
			if (TerrTick > 3)
				TerrTick = 0;
			
			switch (TerrTick) {
				case 0:
					TerrScanVec = new Vector3D(0, -TerrScanSize/2, TerrScanDist);
					break;
				case 1:
					TerrScanVec = new Vector3D(0, TerrScanSize/2, TerrScanDist);
					break;
				case 2:
					TerrScanVec = new Vector3D(-TerrScanSize/2, 0, TerrScanDist);
					break;
				case 3:
					TerrScanVec = new Vector3D(TerrScanSize/2, 0, TerrScanDist);
					break;
			}
			
		if (TerrainCam.CanScan(TerrScanVec.Length())) {
			
			Vector3D HitPosition = TerrScanVec;
			
			double PitchAng = Math.Atan2(TerrScanVec.Y, TerrScanVec.Z);
			double YawAng = Math.Atan2(TerrScanVec.X, TerrScanVec.Z);

			
			var Result = TerrainCam.Raycast((float)TerrScanVec.Length(), (float)((PitchAng - TerrMyPitch)/Math.PI*180), (float)((YawAng + TerrMyRoll)/Math.PI*180));
			
			if (Result.HitPosition != null)
				HitPosition = (Vector3D)Result.HitPosition;
			
			VecCache[TerrTick] = HitPosition;
			
			if (TerrTick == 1) {
				TerrPitch = Math.Asin(Vector3D.Dot(Vector3D.Normalize(TerrCockpit.GetNaturalGravity()), VecCache[0] - VecCache[1])/Vector3D.Distance(VecCache[0], VecCache[1]))/Math.PI*180;
			} else if (TerrTick == 3) {
				TerrRoll = -Math.Asin(Vector3D.Dot(Vector3D.Normalize(TerrCockpit.GetNaturalGravity()), VecCache[2] - VecCache[3])/Vector3D.Distance(VecCache[2], VecCache[3]))/Math.PI*180;
			}
			
			TerrTick ++ ;
		}
	}
}

//以下为测试代码

public void writeLcd(string lcdname, string txt, float size) {
    var lcd = GridTerminalSystem.GetBlockWithName(lcdname) as IMyTextPanel;
    if (lcd != null) {
        lcd.ContentType = ContentType.TEXT_AND_IMAGE;
        lcd.FontSize = size;
        lcd.WriteText(txt);
    } else Echo($"writeLcd: {lcdname} is not exist.\n");
}

byte Tick = 0;

public void Main(string Argument, UpdateType UpdateSource) {
	if (Tick > 3) {
		Tick = 0;
	}
	
	ScanTerrain();
	
	Echo($"{TerrTick}");
	Echo($"1 - {Vector3D.Dot(Vector3D.Normalize(TerrCockpit.GetNaturalGravity()), VecCache[0] - VecCache[1])}");
	Echo($"2 - {Vector3D.Dot(Vector3D.Normalize(TerrCockpit.GetNaturalGravity()), VecCache[2] - VecCache[3])}");
	writeLcd("TerrLCD", $"P {TerrPitch}\nR {TerrRoll}", 1.8f);
	Echo($"P {TerrPitch}\nR {TerrRoll}");
		
	
	Echo($"CurTick: {Tick}");
	Tick ++ ;
	
}
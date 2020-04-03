//VehicleControllerV2 by 次硅酸钙
//20-03-25

const string CockpitName = "Cockpit";			//驾驶舱名称
const string WheelGroupName = "Wheels";			//车轮分组
const string SuspGroupName = "Susp";			//悬架分组
const string BrakeGroupName = "Brake";			//刹车用远程控制分组
const string BatteryName = "Battery";			//电池名称

const string DisableWhenStandby = "Standby";	//待机模式时关闭的分组

//车速设定
static float[] MaxSpeed = {20f, 40f, 70f, 100f, 140f, 180f, 250f};
const float MaxRevSpeed = 20f;

//腾空判定阈值
const double MidAirAccFac = 0.8;
const double MidAirDist = 4;

//偏航修正设定
const double YawFixFac = 2;	
const double YawFixScl = 7.5;
const double YawFixClamp = 5;

//地形扫描摄像机名称
const string TerrainCamFrontName = "Terrain Camera F";
const string TerrainCamRearName = "Terrain Camera R";

const double GearUpMinFac = 0.85;	//升降档阈值设定
const double PowerSmoothing = 25;	//功率平滑程度

const int RefreshTime = 10;			//刷新间隔

//转向设定
static double[,] SteerCurve = {{10, 25}, {18, 12}, {30, 6}, {50, 10}};
static bool UseDiff = true;

//获取方块
void GetBlocks()
{
	
	Wheels.Clear();
	Susps.Clear();
	Brakes.Clear();
	FuncBlockList.Clear();
	
	var AllControllers = new List<IMyShipController>();
	var BlockGroup = GridTerminalSystem.GetBlockGroupWithName(WheelGroupName);
	
	//获取车轮
	if (BlockGroup != null)
		BlockGroup.GetBlocksOfType<IMyMotorSuspension>(Wheels);
	
	//获取悬架
	BlockGroup = GridTerminalSystem.GetBlockGroupWithName(SuspGroupName);
	if (BlockGroup != null)
		BlockGroup.GetBlocksOfType<IMyMotorSuspension>(Susps);
	
	//获取远程控制
	BlockGroup = GridTerminalSystem.GetBlockGroupWithName(BrakeGroupName);
	if (BlockGroup != null)
	{
		AllControllers.Clear();
		BlockGroup.GetBlocksOfType<IMyShipController>(AllControllers);
		foreach (IMyShipController Controller in AllControllers)
			if (Controller is IMyRemoteControl)
				Brakes.Add(Controller);
	}
	
	BlockGroup = GridTerminalSystem.GetBlockGroupWithName(DisableWhenStandby);
	if (BlockGroup != null)
		BlockGroup.GetBlocksOfType<IMyFunctionalBlock>(FuncBlockList);
	
	//获取转子
	CamRotor = GridTerminalSystem.GetBlockWithName("rotor_rear") as IMyMotorStator;
	
	//获取驾驶舱
	Cockpit = GridTerminalSystem.GetBlockWithName(CockpitName) as IMyShipController;
	
	//获取电池
	Battery = GridTerminalSystem.GetBlockWithName(BatteryName) as IMyBatteryBlock;
	
	//获取陀螺仪
	if (Cockpit != null)
	{
		Gyros.Clear();
		//var AllGyros = new List<IMyGyro>();
		GridTerminalSystem.GetBlocksOfType<IMyGyro>(Gyros);
	
		/*
		foreach (IMyGyro Gyro in AllGyros)
			if (Gyro.CustomName.Contains("[B]"))
				Gyros.Add(Gyro);
		*/
			
		InitGyros();
		
		GetTerrainCamera();
	}
}

#region GyroControl
//修改自MEA Ship类陀螺仪部分
List<string> GyroYawField = new List<string>();
List<string> GyroPitchField = new List<string>();
List<string> GyroRollField = new List<string>();
List<float> GyroYawFactor = new List<float>();
List<float> GyroPitchFactor = new List<float>();
List<float> GyroRollFactor = new List<float>();

const double ToRad = Math.PI/180;
const double ToAng = 180/Math.PI;

void InitGyros() {
	
	GyroYawField.Clear();
	GyroPitchField.Clear();
	GyroRollField.Clear();
	GyroYawFactor.Clear();
	GyroPitchFactor.Clear();
	GyroRollFactor.Clear();
	
	foreach (IMyGyro Gyro in Gyros) {
		Base6Directions.Direction GyroUp = Gyro.WorldMatrix.GetClosestDirection(Cockpit.WorldMatrix.Up);   
		Base6Directions.Direction GyroLeft = Gyro.WorldMatrix.GetClosestDirection(Cockpit.WorldMatrix.Left);   
		Base6Directions.Direction GyroForward = Gyro.WorldMatrix.GetClosestDirection(Cockpit.WorldMatrix.Forward);   
		switch (GyroUp) {
			case Base6Directions.Direction.Up: GyroYawField.Add("Yaw"); GyroYawFactor.Add(1f); break;   
			case Base6Directions.Direction.Down: GyroYawField.Add("Yaw"); GyroYawFactor.Add(-1f); break;   
			case Base6Directions.Direction.Left: GyroYawField.Add("Pitch"); GyroYawFactor.Add(1f); break;   
			case Base6Directions.Direction.Right: GyroYawField.Add("Pitch"); GyroYawFactor.Add(-1f); break;   
			case Base6Directions.Direction.Forward: GyroYawField.Add("Roll"); GyroYawFactor.Add(-1f); break;   
			case Base6Directions.Direction.Backward: GyroYawField.Add("Roll"); GyroYawFactor.Add(1f); break;   
		}   
		switch (GyroLeft) {
			case Base6Directions.Direction.Up: GyroPitchField.Add("Yaw"); GyroPitchFactor.Add(1f); break;   
			case Base6Directions.Direction.Down: GyroPitchField.Add("Yaw"); GyroPitchFactor.Add(-1f); break;   
			case Base6Directions.Direction.Left: GyroPitchField.Add("Pitch"); GyroPitchFactor.Add(1f); break;   
			case Base6Directions.Direction.Right: GyroPitchField.Add("Pitch"); GyroPitchFactor.Add(-1f); break;   
			case Base6Directions.Direction.Forward: GyroPitchField.Add("Roll"); GyroPitchFactor.Add(-1f); break;   
			case Base6Directions.Direction.Backward: GyroPitchField.Add("Roll"); GyroPitchFactor.Add(1f); break;  
		}   
		switch (GyroForward) {
			case Base6Directions.Direction.Up: GyroRollField.Add("Yaw"); GyroRollFactor.Add(1f); break;   
			case Base6Directions.Direction.Down: GyroRollField.Add("Yaw"); GyroRollFactor.Add(-1f); break;   
			case Base6Directions.Direction.Left: GyroRollField.Add("Pitch"); GyroRollFactor.Add(1f); break;   
			case Base6Directions.Direction.Right: GyroRollField.Add("Pitch"); GyroRollFactor.Add(-1f); break;   
			case Base6Directions.Direction.Forward: GyroRollField.Add("Roll"); GyroRollFactor.Add(-1f); break;   
			case Base6Directions.Direction.Backward: GyroRollField.Add("Roll"); GyroRollFactor.Add(1f); break;   
		}
	}
}

void SetGyros(double Pitch, double Roll, double Yaw) {
	int i = 0;
    foreach (IMyGyro Gyro in Gyros) {
		if (Gyro != null)
		{
			Gyro.SetValue<bool>("Override", true);
			Gyro.SetValue<float>(GyroPitchField[i], (float)Pitch*GyroPitchFactor[i]);
			Gyro.SetValue<float>(GyroRollField[i], (float)Roll*GyroRollFactor[i]);
			Gyro.SetValue<float>(GyroYawField[i], (float)Yaw*GyroYawFactor[i]);
		}
		i ++ ;
    }
}

//PID部分
class MyPID
{
	//使用https://www.kancloud.cn/kylilimin/mea-ship/995669中的PID函数构建的PID类
	
	readonly int T; //周期，这个周期不是公式中的时间间隔T，而是储存累加结果的周期
	readonly double P; //比例系数
	readonly double I; //积分系数
	readonly double D; //微分系数
	List<double> ErrArray; //储存误差的数组
	
	public double GetPID(double Current, double Target = 0)
	{
		double Diff = Current - Target;
		
		ErrArray.Add(Diff);
		
		if(ErrArray.Count > T)
			ErrArray.Remove(ErrArray[0]);

		double Sum = 0;
		foreach(double Error in ErrArray)
			Sum += Error;

		return P * ((Current - Target) + Sum/I + D*(Diff - ErrArray[ErrArray.Count - 2])); //输出结果
	}
	
	public MyPID(double InitP = .8, double InitI = 50, double InitD = 25, int InitT = 4)
	{
		ErrArray = new List<double>();
		ErrArray.Add(0);
		
		P = InitP;
		I = InitI;
		D = InitD;
		T = InitT;
	}
}
#endregion

#region TerrScanDualCameraMethod
const float TerrScanDist = 40f;	//地形侦测距离
const float TerrScanSize = 30f;	//TerrScanDist处的侦测范围
const float TerrClampAng = 15f; //校正角度限制
//(TerrScanDist > TerrScanSize)

IMyShipController TerrCockpit;
IMyCameraBlock TerrainCamFront;
IMyCameraBlock TerrainCamRear;
Vector3D[] VecCacheF = new Vector3D[2];
Vector3D[] VecCacheR = new Vector3D[2];

double TerrPitch = 0;
double TerrRoll = 0;
double TerrDist = 0;

double TerrMyPitch, TerrMyRoll;

Vector3D TerrScanVecF, TerrScanVecR;

int TerrTick = 0;

//static Vector3D TerrScanVec1 = new Vector3D(0, -TerrScanSize/2, TerrScanDist);
//static Vector3D TerrScanVec2 = new Vector3D(0, TerrScanSize/2, TerrScanDist);
static Vector3D TerrScanVec1 = new Vector3D(0, 0, TerrScanDist);
static Vector3D TerrScanVec2 = new Vector3D(0, 0, TerrScanDist);
static Vector3D TerrScanVec3 = new Vector3D(-TerrScanSize/2, 0, TerrScanDist);
static Vector3D TerrScanVec4 = new Vector3D(TerrScanSize/2, 0, TerrScanDist);

static double TerrScanTotalLenF = TerrScanVec2.Length() + TerrScanVec3.Length() + TerrScanVec4.Length();
static double TerrScanTotalLenR = TerrScanVec1.Length() + TerrScanVec3.Length() + TerrScanVec4.Length();

void GetTerrainCamera()
{
	
	TerrainCamFront = GridTerminalSystem.GetBlockWithName(TerrainCamFrontName) as IMyCameraBlock;
	TerrainCamRear = GridTerminalSystem.GetBlockWithName(TerrainCamRearName) as IMyCameraBlock;
	TerrCockpit = Cockpit;
}

void ScanTerrain()
{
	if (TerrainCamFront != null && TerrainCamRear != null && TerrCockpit != null)
	{
		TerrainCamFront.EnableRaycast = TerrainCamRear.EnableRaycast = true;
		
		if (TerrainCamFront.AvailableScanRange > TerrScanTotalLenF && TerrainCamRear.AvailableScanRange > TerrScanTotalLenR)
		{
			bool NoResult = false;
			
			MatrixD RefLookAtMatrix = MatrixD.CreateLookAt(Vector3D.Zero, TerrCockpit.WorldMatrix.Forward, TerrCockpit.WorldMatrix.Up);
			Vector3D MeToGravity = Vector3D.Normalize(Vector3D.TransformNormal(TerrCockpit.GetNaturalGravity(), RefLookAtMatrix));
			
			double MaxAng = MathHelper.ToRadians(TerrainCamFront.RaycastConeLimit/45*TerrClampAng);
			
			TerrMyPitch = Math.Min(Math.Max(Math.Atan2(MeToGravity.Z, -MeToGravity.Y), -MaxAng), MaxAng);
			TerrMyRoll = Math.Min(Math.Max(Math.Atan2(MeToGravity.X, -MeToGravity.Y), -MaxAng), MaxAng);
			
			TerrTick = 0;
			while (TerrTick < 3)
			{
				MatrixD RefLookAtMatrixCam = MatrixD.CreateLookAt(Vector3D.Zero, TerrainCamFront.WorldMatrix.Forward, TerrainCamFront.WorldMatrix.Up);
				
				switch (TerrTick)
				{
					case 0:	//FR
						TerrScanVecF = TerrScanVec2;
						TerrScanVecR = TerrScanVec1;
						break;
					case 1:	//L
						TerrScanVecF = TerrScanVecR = TerrScanVec3;
						break;
					case 2:	//R
						TerrScanVecF = TerrScanVecR = TerrScanVec4;
						break;
				}
				
				Vector3D HitPositionF = TerrScanVecF;
				Vector3D HitPositionR = TerrScanVecR;
					
				double PitchAngF = Math.Atan2(TerrScanVecF.Y, TerrScanVecF.Z);
				double YawAngF = Math.Atan2(TerrScanVecF.X, TerrScanVecF.Z);
				
				double PitchAngR, YawAngR;
				
				if (TerrTick == 0)
				{
					PitchAngR = Math.Atan2(TerrScanVecR.Y, TerrScanVecR.Z);
					YawAngR = Math.Atan2(TerrScanVecR.X, TerrScanVecR.Z);
				}
				else
				{
					PitchAngR = PitchAngF;
					YawAngR = YawAngF;
				}
					
				var ResultF = TerrainCamFront.Raycast((float)TerrScanVecF.Length(), (float)((PitchAngF - TerrMyPitch)/Math.PI*180), (float)((YawAngF + TerrMyRoll)/Math.PI*180));
				var ResultR = TerrainCamRear.Raycast((float)TerrScanVecR.Length(), (float)((PitchAngR - TerrMyPitch)/Math.PI*180), (float)((YawAngR + TerrMyRoll)/Math.PI*180));
					
				if (ResultF.HitPosition != null && ResultR.HitPosition != null)
				{
					HitPositionF = (Vector3D)ResultF.HitPosition;
					HitPositionR = (Vector3D)ResultR.HitPosition;
				}
				else
				{
					NoResult = true;
					TerrDist = TerrScanDist;
					break;
				}
				
				if (TerrTick > 0)
				{
					VecCacheF[TerrTick - 1] = HitPositionF;
					VecCacheR[TerrTick - 1] = HitPositionR;
				}
				
				if (TerrTick == 0)
				{
					TerrPitch = Math.Asin(Vector3D.Dot(Vector3D.Normalize(TerrCockpit.GetNaturalGravity()), HitPositionR - HitPositionF)/Vector3D.Distance(HitPositionF, HitPositionR))/Math.PI*180;
					TerrDist = Vector3D.Distance(TerrainCamFront.GetPosition(), HitPositionF) + Vector3D.Distance(TerrainCamRear.GetPosition(), HitPositionR);
				}
				else if (TerrTick == 2)
				{
					var VecL_Mid = (VecCacheF[0] + VecCacheR[0])/2;
					var VecR_Mid = (VecCacheF[1] + VecCacheR[1])/2;
					var CamPosMid = (TerrainCamFront.GetPosition() + TerrainCamRear.GetPosition())/2;
					
					TerrRoll = -Math.Asin(Vector3D.Dot(Vector3D.Normalize(TerrCockpit.GetNaturalGravity()), VecL_Mid - VecR_Mid)/Vector3D.Distance(VecL_Mid, VecR_Mid))/Math.PI*180;

					TerrDist += Vector3D.Distance(CamPosMid, VecL_Mid) + Vector3D.Distance(CamPosMid, VecR_Mid);
					TerrDist /= 4;
				}
				
				TerrTick ++ ;
			}
			
			if (double.IsNaN(TerrPitch) || double.IsNaN(TerrRoll)) NoResult = true;
			
			if (NoResult)
				TerrPitch = TerrRoll = 0;
		}
	}
}
#endregion

#region Compass

MyCompass Compass = new MyCompass();

class MyCompass
{
	const bool UseAngOfs = true;
	
	Vector3D AbsNorth = new Vector3D(0, -1, 0);
	
	static string[] HeadingTxt = {"N", "NE", "E", "SE", "S", "SW", "W", "NW", "N"};
	
	public double Angle = 0;
	public string Heading = "";
	public bool Available = false;
	
	public void Update(IMyShipController Controller)
	{
		if (Controller != null)
		{
			var GravityVec = Controller.GetNaturalGravity();
			if (GravityVec.Length() != 0)
			{
				var RefForward = Controller.WorldMatrix.Forward;
				var EastVec = Vector3D.Normalize(GravityVec.Cross(AbsNorth));
				var NorthVec = Vector3D.Normalize(EastVec.Cross(GravityVec));
				var NavLookAt = MatrixD.CreateLookAt(Vector3D.Zero, NorthVec, EastVec);
				
				var HeadingVec = Vector3D.TransformNormal(RefForward, NavLookAt);
				
				Angle = MathHelper.ToDegrees(Math.Atan2(HeadingVec.Y, -HeadingVec.Z)) + (UseAngOfs ? 30 : 0);
				if (Angle < 0) Angle = 360 + Angle;
				if (Angle >= 359.5) Angle -= 360;
				
				Heading = HeadingTxt[(int)Math.Round(Angle/45)];
				
				Available = true;
			}
			else
			{
				Angle = 0;
				Available = false;
			}
		}
		else Available = false;
	}
}

#endregion

#region DrivingControl
//设定车轮参数
void SetWheels(float Power, float TargetSpeed, float Steer, float Speed, bool IsBrake)
{
	foreach (IMyMotorSuspension Wheel in Wheels)
		if (Wheel != null)
		{
			var FinalPower = Power;
			if (UseDiff)
				if (Steer < 0)
					if (Wheel.CustomName.Contains("[L]"))
						FinalPower = Power*0.5f;
				else if (Steer > 0)
					if (Wheel.CustomName.Contains("[R]"))
						FinalPower = Power*0.5f;
				
			Wheel.Power = (float)Math.Abs(FinalPower)*30f;
			Wheel.SetValue<float>("Propulsion override", FinalPower);
			Wheel.SetValue<float>("Speed Limit", TargetSpeed);
			
			string WheelName = Wheel.CustomName;
			
			double SteerFac = 1;
			if (WheelName.Contains("[Q]"))
				SteerFac = 0.25;
			else if (WheelName.Contains("[H]"))
				SteerFac = 0.5;
			
			Wheel.MaxSteerAngle = (float)Math.Abs(Steer*SteerFac/180*Math.PI);
			Wheel.SetValue<float>("Steer override", (float)Math.Sign(Steer));
		}
}

//刹车
void SetBrake(bool IsBrake)
{
	foreach (IMyShipController Brake in Brakes)
		if (Brake != null)
			Brake.HandBrake = IsBrake;
}

//获取当前速度下的转向角（单位：m/s）
double GetSteerAngle(double Speed)
{
	int CurveLeng = SteerCurve.Length/2;
	int i = 0;
	if (Speed < SteerCurve[0,0]) return SteerCurve[0,1];
	while (i < CurveLeng - 1)
	{
		if (Speed < SteerCurve[i+1,0])
		{
			var PrevSpd = SteerCurve[i,0];
			var NextSpd = SteerCurve[i+1,0];
			var PrevAng = SteerCurve[i,1];
			var NextAng = SteerCurve[i+1,1];
			return (Speed - PrevSpd)/(NextSpd - PrevSpd)*(NextAng - PrevAng) + PrevAng;
		}
		i ++ ;
	}
	return SteerCurve[CurveLeng - 1,1];
}

#endregion

#region Screens

class MyGraphFunc
{

	public MySpriteDrawFrame drawframe;
	public Vector2 scrsize;
	public float viewport;
	public float scl;
	
	public MyGraphFunc(MySpriteDrawFrame initdrawframe, float initviewport = 0f, float initscl = 1f)
	{
		drawframe = initdrawframe;
		scrsize = new Vector2(512f, 512f);
		viewport = initviewport;
		scl = initscl;
	}
	
	public MyGraphFunc(MySpriteDrawFrame initdrawframe, Vector2 initscrsize, float initviewport = 0f, float initscl = 1f)
	{
		drawframe = initdrawframe;
		scrsize = initscrsize;
		viewport = initviewport;
		scl = initscl;
	}

	#region GraphicalFunc
	public void LineTo(double x1, double y1, double x2, double y2, float thickness, Color linecolor)
	{
		if (((x1 > 512 && x2 > 512) || (x1 < 0 && x2 < 0)) && ((y1 > 512 && y2 > 512) || (y1 < 0 && y2 < 0))) return;
		float leng = (float)Math.Sqrt((x1-x2)*(x1-x2) + (y1-y2)*(y1-y2));
		float midX = (float)((x1+x2)/2);
		float midY = (float)((y1+y2)/2);
		MySprite line = MySprite.CreateSprite("SquareSimple", new Vector2((midX - 256f)/scl + scrsize.X/2f, (midY - 256f)/scl + scrsize.Y/2f + viewport), new Vector2(leng/scl, thickness/scl));
		line.RotationOrScale = (float)(Math.Sign(x1-x2)*Math.Sign(y1-y2)*Math.Atan(Math.Abs(y1-y2)/Math.Abs(x1-x2)));
		if (x1-x2 == 0) line.RotationOrScale = (float)3.1415926535897932384626433832795/2f;
		line.Color = linecolor;
		drawframe.Add(line);
	}

	public void CircleAt(double x1, double y1, float size, Color circolor)
	{
		if ((x1 > 512 || x1 < 0) && (y1 > 512 || y1 < 0)) return;
		MySprite circle = MySprite.CreateSprite("Circle", new Vector2((float)(x1 - 256f)/scl + scrsize.X/2f, (float)(y1 - 256f)/scl + scrsize.Y/2f + viewport), new Vector2(size/scl, size/scl));
		circle.Color = circolor;
		drawframe.Add(circle);
	}

	public void DoRect(double x1, double y1, double x2, double y2, Color rectcolor)
	{
		if (((x1 > 512 && x2 > 512) || (x1 < 0 && x2 < 0)) && ((y1 > 512 && y2 > 512) || (y1 < 0 && y2 < 0))) return;	
		MySprite rect = MySprite.CreateSprite("SquareSimple", new Vector2((float)(x1+(x2-x1)/2f - 256f)/scl + scrsize.X/2f, (float)(y1+(y2-y1)/2f - 256f)/scl + scrsize.Y/2f + viewport), new Vector2((float)(x2-x1)/scl, (float)(y2-y1)/scl));
		rect.Color = rectcolor;
		drawframe.Add(rect);
	}
	
	public void DoRectHollow(double x1, double y1, double x2, double y2, float thickness, Color rectcolor)
	{
		if (((x1 > 512+thickness && x2 > 512+thickness) || (x1 < -thickness && x2 < -thickness)) && ((y1 > 512+thickness && y2 > 512+thickness) || (y1 < -thickness && y2 < -thickness))) return;	
		DoRect(x1, y1, x2, y1+thickness, rectcolor);
		DoRect(x2-thickness, y1, x2, y2, rectcolor);
		DoRect(x1, y2-thickness, x2, y2, rectcolor);
		DoRect(x1, y1, x1+thickness, y2, rectcolor);
	}
	
	public void DoMask(double x1, double y1, double x2, double y2, Color bgcolor)
	{
		if (((x1 >= 512 && x2 >= 512) || (x1 <= 0 && x2 <= 0)) && ((y1 >= 512 && y2 >= 512) || (y1 <= 0 && y2 <= 0))) return;	
		DoRect(0, 0, 512, y1, bgcolor);
		DoRect(0, 0, x1, 512, bgcolor);
		DoRect(0, y2, 512, 512, bgcolor);
		DoRect(x2, 0, 512, 512, bgcolor);
	}

	public void DoTexture(string texture, double x1, double y1, double x2, double y2, Color sprcolor, double rot = 0)
	{
		if (((x1 > 512 && x2 > 512) || (x1 < 0 && x2 < 0)) && ((y1 > 512 && y2 > 512) || (y1 < 0 && y2 < 0))) return;	
		MySprite spr = MySprite.CreateSprite(texture, new Vector2((float)(x1 - 256f)/scl + scrsize.X/2f, (float)(y1 - 256f)/scl + scrsize.Y/2f + viewport), new Vector2((float)x2/scl, (float)y2/scl));
		spr.Color = sprcolor;
		spr.RotationOrScale = (float)(rot/180*Math.PI);
		drawframe.Add(spr);
	}

	public void DoText(string text, float size, string font, TextAlignment align, double x1, double y1, Color txtcolor)
	{
		//if ((x1 > 512 || x1 < 0) && (y1 > 512 || y1 < 0)) return;
		MySprite txt = MySprite.CreateText(text, font, txtcolor, size/scl, align);
		txt.Position = new Vector2((float)(x1 - 256f)/scl + scrsize.X/2f, (float)(y1 - 256f)/scl + scrsize.Y/2f + viewport);
		drawframe.Add(txt);
	}
	#endregion
}

void CiLogo(IMyTextSurface Surface, float Brightness)
{
	
	MySpriteDrawFrame DrawFrame = Surface.DrawFrame();
	
	Surface.ContentType = ContentType.SCRIPT;
	Surface.Script = "";
	Surface.ScriptBackgroundColor = Surface.ScriptForegroundColor = Color.Black;
	
	Vector2 ScrSize = Surface.SurfaceSize;
	float Scale = 320f/(float)Math.Min(ScrSize.X, ScrSize.Y);
	float Viewport = (Surface.TextureSize.Y - ScrSize.Y)/2f;
	
	int RealBrightness = (int)Math.Round(Brightness/100f*255f);
	Color LogoColor = new Color(RealBrightness, RealBrightness, RealBrightness);
	
	var GraphFunc = new MyGraphFunc(DrawFrame, ScrSize, Viewport, Scale);
	
	float Thickness = (float)Math.Sqrt(968);
	GraphFunc.LineTo(184.5, 213.5, 218.5, 179.5, Thickness, LogoColor);
	GraphFunc.LineTo(184.5, 297.5, 218.5, 331.5, Thickness, LogoColor);
	
	Thickness = (float)Math.Sqrt(648);
	GraphFunc.LineTo(234, 177, 269, 212, Thickness, LogoColor);
	GraphFunc.LineTo(234, 334, 269, 299, Thickness, LogoColor);
	
	GraphFunc.DoRect(173, 203, 197, 308.5, LogoColor);
	GraphFunc.DoRect(208, 168, 243, 203, LogoColor);
	GraphFunc.DoRect(207, 308, 243, 343, LogoColor);
	
	GraphFunc.DoRect(314, 168, 337, 203, LogoColor);
	GraphFunc.DoRect(314, 238, 337, 343, LogoColor);
	
	GraphFunc.DoRect(214, 220, 277, 291, Color.Black);
	GraphFunc.DoRect(277, 168, 313, 343, Color.Black);
	
	DrawFrame.Dispose();
}

void SetScreenColor(IMyTextSurface Surface, float Brightness)
{
	Surface.ScriptBackgroundColor = Color.Black;
	int RealBrightness = (int)Math.Round(Brightness/100f*255f);
	Color FinalColorWhite = new Color(RealBrightness, RealBrightness, RealBrightness);
	Surface.ScriptForegroundColor = FinalColorWhite;
}

void ClearScreen(IMyTextSurface Surface)
{
	MySpriteDrawFrame DrawFrame = Surface.DrawFrame();
	
	Surface.ScriptBackgroundColor = Surface.ScriptForegroundColor = Color.Black;
	
	DrawFrame.Dispose();
}

double BrightnessTimer = 0;

//主屏幕
void DrawMainScreen(IMyTextSurface Surface, float Brightness)
{
	MySpriteDrawFrame DrawFrame = Surface.DrawFrame();
	
	Surface.ContentType = ContentType.SCRIPT;
	Surface.Script = "";
	Surface.ScriptBackgroundColor = Surface.ScriptForegroundColor = Color.Black;
	
	Vector2 ScrSize = Surface.SurfaceSize;
//	float Scale = 512f/(float)Math.Min(ScrSize.X, ScrSize.Y)/3f;
	float Scale = 512f/ScrSize.X/2f;
	float Viewport = (Surface.TextureSize.Y - ScrSize.Y)/2f;
	
	var GraphFunc = new MyGraphFunc(DrawFrame, ScrSize, Viewport, Scale);
	
	int RealBrightness = (int)Math.Round(Brightness/100f*255f);
	Color FinalColorWhite = new Color(RealBrightness, RealBrightness, RealBrightness);
	Color FinalColorGray = new Color(RealBrightness/2, RealBrightness/2, RealBrightness/2);
	Color FinalColorClock = new Color(RealBrightness/5*4, RealBrightness/5*4, RealBrightness/5*4);
	Color FinalColorRed = new Color(RealBrightness, RealBrightness/6, RealBrightness/6);
	
	string GearTxt = ((IsHandbrake && CockpitSpeed < 2) ? "N" : (IsReverse ? "R" : $"{Gear+1}"));
	
	string HintTxt = (((CockpitSpeed*3.6 > CurMaxSpd*GearUpMinFac) && !IsHandbrake && (Gear != MaxSpeed.Length - 1) && (MoveIndicator.Y <= 0) && (MoveIndicator.Z <= 0)) ? "Gear up" : (IsHandbrake && CockpitSpeed < 2 ? "Handbrake" : ""));
	
	//“转速表”
	GraphFunc.CircleAt(260, 300, 226f, FinalColorWhite);
	GraphFunc.CircleAt(260, 300, 224f, Color.Black);
	GraphFunc.CircleAt(260, 300, 120f, FinalColorWhite);
	GraphFunc.CircleAt(260, 300, 118f, Color.Black);
	GraphFunc.DoRect(143, 321, 377, 512, Color.Black);
	GraphFunc.DoRect(346, 265, 377, 512, Color.Black);
	
	GraphFunc.DoRect(218, 216, 304, 236, FinalColorGray);
	GraphFunc.DoRect(220, 218, 302, 234, Color.Black);
	
	string TimeStr = DateTime.Now.ToLocalTime().ToString("HH:mm:ss");
	GraphFunc.DoText($"{TimeStr}", .5f, "Monospace", TextAlignment.CENTER, 261, 218, FinalColorClock);
	
	double TargetAng = Math.PI + Math.Min(Math.Abs(CockpitSpeed)*3.6, MaxSpeed[Gear])/MaxSpeed[Gear] * (Math.PI * .85);
	if (IsHandbrake && MoveIndicator.Z != 0 && CockpitSpeed < 2)
		TargetAng = Math.PI*1.85;
	
	NeedleAng -= (NeedleAng - TargetAng)/1.6;
	
	int i = 0;
	while (i < 9)
	{
		double Ang = 2.96705972839036 + Math.PI * .92 * (i/8.0);
		GraphFunc.DoText($"{i}", .7f, "Debug", TextAlignment.CENTER, 260 + Math.Cos(Ang)*100, 287 + Math.Sin(Ang)*100, FinalColorWhite);
		i ++ ;
	}
	
	double Sin = Math.Sin(NeedleAng);
	double Cos = Math.Cos(NeedleAng);
	
	GraphFunc.LineTo(260 +  + Cos*60, 300 + Sin*60, 260 + Cos*100, 300 + Sin*100, 4f, FinalColorRed);
	GraphFunc.CircleAt(260 + Cos*100, 300 + Sin*100, 4f, FinalColorRed);
	
	//速度，档位
	GraphFunc.DoRect(270, 279, 372, 321, FinalColorWhite);
	GraphFunc.DoRect(271, 280, 371, 320, Color.Black);
	GraphFunc.DoRect(276, 284, 299, 316, FinalColorGray);
	GraphFunc.DoRect(277, 285, 298, 315, Color.Black);
	
	GraphFunc.DoText($"{Math.Round(CockpitSpeed*3.6)}", 1f, "Debug", TextAlignment.CENTER, 335, 284, FinalColorWhite);
	GraphFunc.DoText($"{GearTxt}", .9f, "Debug", TextAlignment.CENTER, 288, 287, FinalColorWhite);
	GraphFunc.DoText($"{HintTxt}", .6f, "Debug", TextAlignment.CENTER, 324, 323, FinalColorGray);
	
	DrawFrame.Dispose();
}

//姿态
void DrawNavgationScreen(IMyTextSurface Surface, float Brightness)
{
	MySpriteDrawFrame DrawFrame = Surface.DrawFrame();
	
	Surface.ContentType = ContentType.SCRIPT;
	Surface.Script = "";
	Surface.ScriptBackgroundColor = Surface.ScriptForegroundColor = Color.Black;
	
	Vector2 ScrSize = Surface.SurfaceSize;
	float Scale = 512f/(float)Math.Min(ScrSize.X, ScrSize.Y)/3.5f;
	float Viewport = (Surface.TextureSize.Y - ScrSize.Y)/2f;
	
	var GraphFunc = new MyGraphFunc(DrawFrame, ScrSize, Viewport, Scale);
	
	int RealBrightness = (int)Math.Round(Brightness/100f*255f);
	Color FinalColorWhite = new Color(RealBrightness, RealBrightness, RealBrightness);
	Color FinalColorRed = new Color(RealBrightness, RealBrightness/6, RealBrightness/6);
	
	Compass.Update(Cockpit);
	
	if (Compass.Available)
	{
		//Horizon
		const double PitchFac = 4;
		
		var RollToRad = Roll/180*Math.PI;
		var RollSin = Math.Sin(RollToRad);
		var RollCos = Math.Cos(RollToRad);
		
		GraphFunc.LineTo(255 - RollCos*384 + RollSin*Pitch*PitchFac, 273 + RollSin*384 + RollCos*Pitch*PitchFac, 255 + RollCos*384 + RollSin*Pitch*PitchFac, 273 - RollSin*384 + RollCos*Pitch*PitchFac, 2, FinalColorWhite);
		
		int i = 0;
		while (i < 19)
		{
			var TexName = i > 9 ? "AH_GravityHudPositiveDegrees" : "AH_GravityHudNegativeDegrees";
			if (i != 9)
			{
				var PX = 255 + RollSin*(Pitch - (i-9)*10)*PitchFac;
				var PY = 273 + RollCos*(Pitch - (i-9)*10)*PitchFac;
				GraphFunc.DoTexture(TexName, PX, PY, 100, 10, FinalColorWhite, -Roll);
				GraphFunc.DoText($"{Math.Abs((i-9)*10)}", .5f, "Debug", TextAlignment.CENTER, PX, PY - 7, FinalColorWhite);
				GraphFunc.DoTexture("Triangle", 255, 278, 10, 10, FinalColorWhite);
			}
			
			i ++ ;
		}
		
		GraphFunc.DoText($"{Math.Abs(Math.Round(Roll))}°", .7f, "Debug", TextAlignment.RIGHT, 238, 263, FinalColorWhite);
		
		GraphFunc.DoMask(165, 228, 346, 319, Color.Black);
		GraphFunc.DoRectHollow(165, 228, 346, 319, 2, FinalColorWhite);
		
		//Compass
		const string HeadingTxt = "-----  N  ----- N.E -----  E  ----- S.E -----  S  ----- S.W -----  W  ----- N.W -----  N  -----";
		
		var TxtOfs = Compass.Angle/360*1090;
		GraphFunc.DoText(HeadingTxt, .7f, "Monospace", TextAlignment.LEFT, 155 - TxtOfs, 180, FinalColorWhite);
		GraphFunc.DoTexture("Triangle", 255, 211, 12, 12, FinalColorWhite);
		
		GraphFunc.DoText($"{Math.Round(Compass.Angle)}°", .8f, "Debug", TextAlignment.RIGHT, 245, 200, FinalColorWhite);
		GraphFunc.DoText(Compass.Heading, .8f, "Debug", TextAlignment.LEFT, 268, 200, FinalColorWhite);
	}
	else GraphFunc.DoText("NOT AVAILABLE", .8f, "Debug", TextAlignment.CENTER, 255, 240, FinalColorRed);
		
	
	DrawFrame.Dispose();
}

//电量
void DrawBatteryScreen(IMyTextSurface Surface, float Brightness)
{
	MySpriteDrawFrame DrawFrame = Surface.DrawFrame();
	
	Surface.ContentType = ContentType.SCRIPT;
	Surface.Script = "";
	Surface.ScriptBackgroundColor = Surface.ScriptForegroundColor = Color.Black;
	
	Vector2 ScrSize = Surface.SurfaceSize;
	float Scale = 512f/(float)Math.Min(ScrSize.X, ScrSize.Y)/3.5f;
	float Viewport = (Surface.TextureSize.Y - ScrSize.Y)/2f;
	
	var GraphFunc = new MyGraphFunc(DrawFrame, ScrSize, Viewport, Scale);
	
	int RealBrightness = (int)Math.Round(Brightness/100f*255f);
	int RealBrightnessTimed = (int)Math.Round((85 + Brightness/100*170)*(0.5 + Math.Abs(Math.Sin(BrightnessTimer))/2));
	Color FinalColorWhite = new Color(RealBrightness, RealBrightness, RealBrightness);
	Color FinalColorGray = new Color(RealBrightness/2, RealBrightness/2, RealBrightness/2);
	Color FinalColorGrayDark = new Color(RealBrightness/3, RealBrightness/3, RealBrightness/3);
	Color FinalColorRed = new Color(RealBrightness, RealBrightness/6, RealBrightness/6);
	Color NotAllFadeWhite = new Color(85 + RealBrightness*2/3, 85 + RealBrightness*2/3, 85 + RealBrightness*2/3);
	Color NotAllFadeRedTimed = new Color(RealBrightnessTimed, RealBrightnessTimed/6, RealBrightnessTimed/6);
	
	//GraphFunc.DoText("Battery", 1f, "Debug", TextAlignment.LEFT, 135, 174, FinalColorWhite);
	
	if (Battery != null)
	{	
		//Battery Icon
		var OffsetY = 20.0 - Brightness/100.0*20.0;
		
		Color BatteryColor = ((Battery.CurrentStoredPower/Battery.MaxStoredPower) < 0.2 ? NotAllFadeRedTimed : NotAllFadeWhite);
		
		GraphFunc.DoRect(166, 220 + OffsetY, 280, 248 + OffsetY, BatteryColor);
		GraphFunc.DoRect(280, 227 + OffsetY, 284, 241 + OffsetY, BatteryColor);
		GraphFunc.DoRect(168, 222 + OffsetY, 278, 246 + OffsetY, Color.Black);
	
		double BateryRemainingPercent = Battery.CurrentStoredPower/Battery.MaxStoredPower;
		
		double BarEndX = 170 + BateryRemainingPercent*106;
		
		GraphFunc.DoRect(170, 224 + OffsetY, BarEndX, 244 + OffsetY, BatteryColor);
		
		GraphFunc.DoText($"{Math.Round(BateryRemainingPercent*100)}%", .8f, "Debug", TextAlignment.RIGHT, 345, 225 + OffsetY, BatteryColor);
		
		if (Battery.CurrentInput > 0)
		{
			GraphFunc.DoTexture("IconEnergy", 223, 234 + OffsetY, 20, 20, (Math.Abs(Math.Sin(BrightnessTimer)) > 0.6 ? NotAllFadeWhite : Color.Black));
			GraphFunc.DoText("Charging", .7f, "Debug", TextAlignment.LEFT, 166, 265 + OffsetY, FinalColorWhite);
		}
		else
			GraphFunc.DoText("Time\nRemaining", .6f, "Debug", TextAlignment.LEFT, 166, 257 + OffsetY, FinalColorWhite);

		float BatteryOutput = (Battery as IMyPowerProducer).CurrentOutput;
		
		string TimeStr = "";
		int TimeRemaining = 0;
		
		if (BatteryOutput - Battery.CurrentInput > 0)
		{
			TimeRemaining = (int)(Battery.CurrentStoredPower*3600/BatteryOutput);
		}
		else
		{
			TimeRemaining = (int)(((Battery.MaxStoredPower - Battery.CurrentStoredPower)*3600)/(Battery.CurrentInput - BatteryOutput));
		}
		
		if (TimeRemaining > 3600)
			TimeStr = $"{TimeRemaining/3600}h {(TimeRemaining/60)%60}m";
		else if (TimeRemaining > 60)
			TimeStr = $"{TimeRemaining/60}m {TimeRemaining%60}s";
		else
			TimeStr = $"{TimeRemaining}s";
		
		GraphFunc.DoText(TimeStr, .8f, "Debug", TextAlignment.RIGHT, 345, 265 + OffsetY, FinalColorWhite);
	}
	else
	{
		GraphFunc.DoText("NO BATTERY", .8f, "Debug", TextAlignment.CENTER, 255, 240, FinalColorRed);
	}
	
	//GraphFunc.DoTexture("DecorativeBracketLeft", 150, 260, 30, 100, FinalColorGray);
	//GraphFunc.DoTexture("DecorativeBracketRight", 360, 260, 30, 100, FinalColorGray);
	
	DrawFrame.Dispose();
}

//处理仪表
void ProcessDisplay()
{
	BrightnessTimer += Math.PI/8;
	
	var CockpitSurfaces = Cockpit as IMyCockpit;
	
	if (UnderControlTimer < 12 && IsUnderControl && !Standby)
		UnderControlTimer ++ ;
	
	if (UnderControlTimer > 5 && (!IsUnderControl || Standby))
		UnderControlTimer -- ;
	
	if (UnderControlTimer <= 6)
	{
		ClearScreen(CockpitSurfaces.GetSurface(1));
		ClearScreen(CockpitSurfaces.GetSurface(2));
		//CiLogo(CockpitSurfaces.GetSurface(0), ((float)Math.Max((6 - UnderControlTimer)/6f*100f, 0)));
	}
	
	float Brightness = (float)Math.Max((UnderControlTimer - 6)/6.0*100, 0);
	
	DrawBatteryScreen(CockpitSurfaces.GetSurface(0), Brightness);
	
	if (UnderControlTimer >= 6)
	{
		DrawMainScreen(CockpitSurfaces.GetSurface(1), Brightness);
		DrawNavgationScreen(CockpitSurfaces.GetSurface(2), Brightness);
		SetScreenColor(CockpitSurfaces.GetSurface(3), Brightness);
	}
}
#endregion

#region RotorControl
float CalcRotorSpeed(float CurrentAngle, float TargetAngle, float Scale) {
	if (Math.Abs(CurrentAngle - TargetAngle) > 180f) 
		return -(CurrentAngle-TargetAngle+(CurrentAngle > TargetAngle ? -360f : 360f))/Scale;
	return -(CurrentAngle-TargetAngle)/Scale;
}

void RotorSetAng(IMyMotorStator Rotor, double Angle) {
    if (Rotor != null) {
        Rotor.SetValue<float>("Velocity", CalcRotorSpeed(MathHelper.ToDegrees(Rotor.Angle), (float)(Angle/Math.PI*180), 2f));
    }
}
#endregion

#region MainVariables
List<IMyMotorSuspension> Wheels = new List<IMyMotorSuspension>();
List<IMyMotorSuspension> Susps = new List<IMyMotorSuspension>();
List<IMyShipController> Brakes = new List<IMyShipController>();
List<IMyFunctionalBlock> FuncBlockList = new List<IMyFunctionalBlock>();
List<IMyGyro> Gyros = new List<IMyGyro>();

IMyMotorStator CamRotor;

IMyBatteryBlock Battery;

IMyShipController Cockpit;

MyPID PitchPID, RollPID, YawPID;

int Gear = 0;
double Pitch, Roll, Elevation, ElevationOld;
bool Midair;
double RawSpeed, Sliding, GravityLength, VertSpd, VertAccel, VertSpdOld;
float Speed, CockpitSpeed, CurMaxSpd;
float PowerFactor = 0;

Vector3D MeToGround, VehicleSpeed;
Vector3 MoveIndicator;
Vector2 RotationIndicator;
float RollIndicator;

double NeedleAng = Math.PI;

bool IsUnderControl;
bool IsHandbrake;
bool IsReverse;
bool GearDownReady = false;
bool KeyDown = false;
bool Standby = true;
bool UpdateScreen = false;

int UnderControlTimer = 5;

int RefreshTimer = 0;
double ExcuteTime = 0;
#endregion

//处理主要信息
void ProcessMain()
{
	
	Cockpit = GridTerminalSystem.GetBlockWithName(CockpitName) as IMyShipController;
	if (Cockpit != null)
	{
		#region Physical
		//获取玩家输入
		MoveIndicator = Cockpit.MoveIndicator;
		RotationIndicator = Cockpit.RotationIndicator;
		RollIndicator = Cockpit.RollIndicator;
		IsUnderControl = Cockpit.IsUnderControl;
		
		//获取相对地面方向
		MatrixD RefLookAtMatrix = MatrixD.CreateLookAt(Vector3D.Zero, Cockpit.WorldMatrix.Forward, Cockpit.WorldMatrix.Up);
		MeToGround = Vector3D.Normalize(Vector3D.TransformNormal(Cockpit.GetNaturalGravity(), RefLookAtMatrix));
		GravityLength = Vector3D.TransformNormal(Cockpit.GetNaturalGravity(), RefLookAtMatrix).Length();
		
		if (GravityLength != 0)
		{
			Pitch = Math.Atan2(MeToGround.Z, -MeToGround.Y)*ToAng;
			Roll = Math.Atan2(MeToGround.X, -MeToGround.Y)*ToAng;
			
			Cockpit.TryGetPlanetElevation(MyPlanetElevation.Sealevel, out Elevation);
			if (ElevationOld == -1000)
				ElevationOld = Elevation;
		}
		else
		{
			ElevationOld = Elevation = -1000;
			Pitch = Roll = 0;
		}
		
		ScanTerrain();
		
		//获取相对地面速度
		VehicleSpeed = Vector3D.TransformNormal(Cockpit.GetShipVelocities().LinearVelocity, RefLookAtMatrix);
		RawSpeed = -VehicleSpeed.Z;
		Speed = (float)Math.Abs(VehicleSpeed.Z);
		CockpitSpeed = (float)Cockpit.GetShipSpeed();
		
		//Sliding = Math.Round(VehicleSpeed.X*Math.Min(Math.Max((CockpitSpeed > 20f ? CockpitSpeed : RawSpeed)/5, -10), 10));
		//Sliding = Math.Round(VehicleSpeed.X*(CockpitSpeed > 20f ? 1 : Math.Sign(RawSpeed))*YawFixFac);
		Sliding = Math.Round(VehicleSpeed.X*Math.Min(Math.Max((CockpitSpeed > 20f ? CockpitSpeed : RawSpeed)/YawFixScl, -YawFixClamp), YawFixClamp)*YawFixFac);
		
		VertSpd = (Elevation - ElevationOld)*60;
		
		VertAccel = (VertSpd - VertSpdOld)*60;
		
		Midair = -VertAccel > GravityLength*0.8 && TerrDist > MidAirDist;
		
		#endregion
		
		//车身稳定
		SetGyros(PitchPID.GetPID(Math.Round(-(Pitch - TerrPitch)*5)),RollPID.GetPID(Math.Round((Roll - TerrRoll)*5)), YawPID.GetPID(Sliding));
		
		if (!IsUnderControl && CockpitSpeed < 2)
			Standby = true;
		
		if (RawSpeed < 0 && MoveIndicator.Z > 0)
			IsReverse = true;
	
		if ((RawSpeed > 0 && MoveIndicator.Z < 0 && CockpitSpeed*3.6 < MaxRevSpeed*1.5) || (IsHandbrake && CockpitSpeed < 2))
			IsReverse = false;
		
		ElevationOld = Elevation;
		VertSpdOld = VertSpd;
		
		if (!Standby)
		{
			#region Transmission
			
			//变速
			if (MoveIndicator.Y < 0)
			{
				if (!KeyDown)
				{
					if ((CockpitSpeed*3.6 > MaxSpeed[Gear]*GearUpMinFac) && (Gear < MaxSpeed.Length-1))
					{
						Gear ++ ;
						GearDownReady = false;
					}
				}
				KeyDown = true;
			}
			else
				KeyDown = false;
			
			if (Gear > 0)
			{
				if (RawSpeed*3.6f > MaxSpeed[Gear-1] + 5f)
					GearDownReady = true;
				
				if (RawSpeed*3.6f < MaxSpeed[Gear-1]*(GearDownReady ? 0.98f : GearUpMinFac) | ((MoveIndicator.Y > 0) && !GearDownReady))
					Gear -- ;
			}
			
			#endregion
			
			if (MoveIndicator.Z != 0)
				PowerFactor = (float)Math.Max(Math.Min(PowerFactor - (PowerFactor - MoveIndicator.Z*2)/PowerSmoothing, 1), -1);
			else
				PowerFactor = 0;
		
			//动态限速以尽量防止车轮加速时打滑
			CurMaxSpd = MoveIndicator.Z > 0 ? MaxRevSpeed : MaxSpeed[Gear];
			float RealMaxSpd = (float)Math.Min(Speed*3.6*1.096+20, CurMaxSpd*1.096);
			
			if (CockpitSpeed > 10 && TerrDist > MidAirDist)
				PowerFactor = 0;
			
			//刹车
			var EnableSpeedLimit = true;
			if (Math.Sign(-MoveIndicator.Z) != Math.Sign(RawSpeed) && MoveIndicator.Z != 0)
				EnableSpeedLimit = false;
			
			IsHandbrake = Cockpit.HandBrake;
			bool IsBrake = (Midair)|(MoveIndicator.Y > 0)|IsHandbrake|(RawSpeed*3.6f > CurMaxSpd*(RawSpeed*3.6f > 100f ? 1.01f : 1f) && EnableSpeedLimit)|(RawSpeed*3.6f < -MaxRevSpeed);
			SetBrake(IsBrake);
			
			//前进/后退
			float Power = (1f/((Speed/MaxSpeed[MaxSpeed.Length-1]*3.6f)*100f+75f)*75f);
			float Steer = (float)GetSteerAngle(Speed)*MoveIndicator.X;
			
			SetWheels(Power*PowerFactor, RealMaxSpd, Steer, (float)RawSpeed, IsBrake);
			
			if (IsReverse)
				RotorSetAng(CamRotor, Steer/180*Math.PI*(0.25+CockpitSpeed/7));
			else
				RotorSetAng(CamRotor, 0);
			
		}
		else
		{
			Gear = 0;
			PowerFactor = 0;
			SetBrake(true);
			SetWheels(0, 0, 0, 0, true);
			RotorSetAng(CamRotor, 0);
		}
		
		//屏幕
		if (UpdateScreen)
			ProcessDisplay();
		
	}
	else
	{
		Gear = 0;
		SetGyros(0, 0, 0);
		UnderControlTimer = 0;
		Echo("No cockpit Detected");
	}
}

void WriteLCD(string Name, string Text, float Size = -1f)
{
    var LCD = GridTerminalSystem.GetBlockWithName(Name) as IMyTextPanel;
    if (LCD != null)
	{
        LCD.ContentType = ContentType.TEXT_AND_IMAGE;
        if (Size > 0)
			LCD.FontSize = Size;
        LCD.WriteText(Text);
    }
	else Echo($"WriteLCD: {Name} is not exist.\n");
}

public Program()
{
	Runtime.UpdateFrequency = UpdateFrequency.Update1 | UpdateFrequency.Update10;
	GetBlocks();
	PitchPID = new MyPID();
	RollPID = new MyPID();
	YawPID = new MyPID();
}

public void Main(string Argument, UpdateType UpdateSource)
{
	
	switch (Argument)
	{
		case "Standby":
			Standby = !Standby;
			foreach (IMyFunctionalBlock Block in FuncBlockList)
				Block.Enabled = !Standby;
			
			if (Cockpit.HandBrake && !Standby)
				Cockpit.HandBrake = false;
			
			break;
		default:
			break;
	}
	
	Echo($"-- Vehicle Controller V2 --\nby 次硅酸钙\n");
	
	UpdateScreen = ((UpdateSource & UpdateType.Update10) != 0);
	
	if ((UpdateSource & UpdateType.Update1) != 0)
	{
		ProcessMain();
		if (RefreshTimer > RefreshTime*60)
		{
			RefreshTimer = 0;
			GetBlocks();
		}
		//Echo($"MP: {Pitch}\nMR: {Roll}\nTP: {TerrPitch}\nTR: {TerrRoll}\nTD: {TerrDist}");
		Echo($"Refresh in {RefreshTime - (RefreshTimer++)/60} seconds.");
	}
	Echo($"Instruction count: {Runtime.CurrentInstructionCount.ToString()} / {Runtime.MaxInstructionCount.ToString()}\nExcute time : {Math.Round(ExcuteTime = Math.Max(ExcuteTime - (ExcuteTime - Runtime.LastRunTimeMs)/20, Runtime.LastRunTimeMs), 4)}ms");
}
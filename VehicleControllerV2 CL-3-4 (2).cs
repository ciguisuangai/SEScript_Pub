//VehicleControllerV2 by 次硅酸钙
//20-03-14

const string CockpitName = "Cockpit";			//驾驶舱名
const string WheelGroupName = "Wheels";			//车轮分组名
const string SuspGroupName = "Susp";			//悬架分组名
const string BrakeGroupName = "Brake";			//刹车用远程控制分组名
const string BatteryName = "Battery";			//电池名称
const string DisableWhenStandby = "Standby";	//待机模式时关闭的方块分组
float[] MaxSpeed = new float[7]{20f, 40f, 70f, 100f, 140f, 180f, 250f};			//各档位最大车速
const float MaxRevSpeed = 20f;					//最大倒车速度
const float FrontToe = 0f;						//前轮束角（废弃）

const float GearUpMinFac = 0.85f;
const float PowerSmoothing = 25f;

const int RefreshTime = 10;	//刷新间隔时间（单位：秒）

//速度转向角曲线设定	（速度(m/s), 角度）
double[,] SteerCurve = new double[4,2]{{10, 25}, {18, 12}, {30, 6}, {50, 10}};

List<IMyMotorSuspension> Wheels = new List<IMyMotorSuspension>();
List<IMyMotorSuspension> Susps = new List<IMyMotorSuspension>();
List<IMyShipController> Brakes = new List<IMyShipController>();
List<IMyFunctionalBlock> FuncBlockList = new List<IMyFunctionalBlock>();
List<IMyGyro> Gyros = new List<IMyGyro>();

IMyMotorStator RotorFL, RotorFR, RotorRA, RotorRB, CamRotor;

IMyBatteryBlock Battery;

IMyShipController Cockpit;

MyPID PitchPID, RollPID, YawPID;

int Gear = 0;
double RawSpeed, GravityLength;
double Pitch, Roll;
float Speed, CockpitSpeed, CurMaxSpd;
float PowerFac = 0;

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

//获取所需方块
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
	
	//获取悬架转子
	RotorFL = GridTerminalSystem.GetBlockWithName("rotor_susp_FL") as IMyMotorStator;
	RotorFR = GridTerminalSystem.GetBlockWithName("rotor_susp_FR") as IMyMotorStator;
	RotorRA = GridTerminalSystem.GetBlockWithName("rotor_susp_RA") as IMyMotorStator;
	RotorRB = GridTerminalSystem.GetBlockWithName("rotor_susp_RB") as IMyMotorStator;
	CamRotor = GridTerminalSystem.GetBlockWithName("rotor_rear") as IMyMotorStator;
	
	//获取驾驶舱
	Cockpit = GridTerminalSystem.GetBlockWithName(CockpitName) as IMyShipController;
	
	//获取电池
	Battery = GridTerminalSystem.GetBlockWithName(BatteryName) as IMyBatteryBlock;
	
	//获取陀螺仪
	if (Cockpit != null)
	{
		Gyros.Clear();
		var AllGyros = new List<IMyGyro>();
		GridTerminalSystem.GetBlocksOfType<IMyGyro>(AllGyros);
	
		foreach (IMyGyro Gyro in AllGyros)
			if (Gyro.CustomName.Contains("[B]"))
				Gyros.Add(Gyro);
			
		InitGyros();
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
	
	public MyPID(double InitP = 1.2, double InitI = 100, double InitD = 20, int InitT = 5)
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

#region DrivingControl
//设定车轮参数
void SetWheels(float Power, float TargetSpeed, float Steer, float Speed, bool IsBrake)
{
	
	float SteerFac = 1f + (float)Math.Abs(Speed/100f);
	float SteerAngL = MathHelper.ToRadians(Steer - FrontToe*SteerFac);
	float SteerAngR = MathHelper.ToRadians(Steer + FrontToe*SteerFac);
	
	
	foreach (IMyMotorSuspension Wheel in Wheels)
		if (Wheel != null)
		{
			Wheel.Power = (float)Math.Abs(Power)*30f;
			Wheel.SetValue<float>("Propulsion override", Power);
			Wheel.SetValue<float>("Speed Limit", TargetSpeed);
			
			string WheelName = Wheel.CustomName;
			if (WheelName.Contains("[LS]"))
			{
				Wheel.MaxSteerAngle = (float)Math.Abs(SteerAngL);
				Wheel.SetValue<float>("Steer override", (float)Math.Sign(SteerAngL));
			}
			
			if (WheelName.Contains("[RS]"))
			{
				Wheel.MaxSteerAngle = (float)Math.Abs(SteerAngR);
				Wheel.SetValue<float>("Steer override", (float)Math.Sign(SteerAngR));
			}
			
		}
}

//刹车
void SetBrake(bool IsBrake)
{
	foreach (IMyShipController Brake in Brakes)
		if (Brake != null)
		{
			var Handbrake = Brake.HandBrake;
			while (Handbrake != IsBrake)
			{
				Brake.ApplyAction("HandBrake");
				Handbrake = Brake.HandBrake;
			}
		}
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

	public void DoTexture(string texture, double x1, double y1, double x2, double y2, Color sprcolor)
	{
		if (((x1 > 512 && x2 > 512) || (x1 < 0 && x2 < 0)) && ((y1 > 512 && y2 > 512) || (y1 < 0 && y2 < 0))) return;	
		MySprite spr = MySprite.CreateSprite(texture, new Vector2((float)(x1 - 256f)/scl + scrsize.X/2f, (float)(y1 - 256f)/scl + scrsize.Y/2f + viewport), new Vector2((float)x2/scl, (float)y2/scl));
		spr.Color = sprcolor;
		drawframe.Add(spr);
	}

	public void DoText(string text, float size, string font, TextAlignment align, double x1, double y1, Color txtcolor)
	{
		if ((x1 > 512 || x1 < 0) && (y1 > 512 || y1 < 0)) return;
		MySprite txt = MySprite.CreateText(text, font, txtcolor, size/scl, align);
		txt.Position = new Vector2((float)(x1 - 256f)/scl + scrsize.X/2f, (float)(y1 - 256f)/scl + scrsize.Y/2f + viewport);
		drawframe.Add(txt);
	}
	#endregion
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
	float Scale = 512f/(float)Math.Min(ScrSize.X, ScrSize.Y)/3f;
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
	GraphFunc.CircleAt(260, 300, 120f, FinalColorGray);
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

//悬挂状态
void DrawSuspScreen(IMyTextSurface Surface, float Brightness)
{
	MySpriteDrawFrame DrawFrame = Surface.DrawFrame();
	
	Surface.ContentType = ContentType.SCRIPT;
	Surface.Script = "";
	Surface.ScriptBackgroundColor = Surface.ScriptForegroundColor = Color.Black;
	
	Vector2 ScrSize = Surface.SurfaceSize;
	float Scale = 512f/(float)Math.Min(ScrSize.X, ScrSize.Y)/2.8f;
	float Viewport = (Surface.TextureSize.Y - ScrSize.Y)/2f;
	
	var GraphFunc = new MyGraphFunc(DrawFrame, ScrSize, Viewport, Scale);
	
	int RealBrightness = (int)Math.Round(Brightness/100f*255f);
	Color FinalColorWhite = new Color(RealBrightness, RealBrightness, RealBrightness);
	Color FinalColorGray = new Color(RealBrightness/2, RealBrightness/2, RealBrightness/2);
	Color FinalColorGrayDark = new Color(RealBrightness/3, RealBrightness/3, RealBrightness/3);
	Color FinalColorRed = new Color(RealBrightness, RealBrightness/6, RealBrightness/6);
	
	GraphFunc.DoText("Suspension", 1f, "Debug", TextAlignment.LEFT, 135, 174, FinalColorWhite);
	GraphFunc.DoText("F", 1f, "Debug", TextAlignment.CENTER, 197, 224, FinalColorWhite);
	GraphFunc.DoText("R", 1f, "Debug", TextAlignment.CENTER, 197, 293, FinalColorWhite);
	
	//前悬
	if (RotorFL != null && RotorFR != null)
	{
		GraphFunc.LineTo(235, 240, 315, 240, 2, FinalColorGrayDark);
		
		double RotorAng = RotorFL.Angle + RotorFR.Angle;
		
		double PX = 275 - Math.Cos(RotorAng)*30;
		double PY = 240 + Math.Sin(RotorAng)*30;
		
		GraphFunc.LineTo(275, 240, PX, PY, 3, FinalColorWhite);
		
		double Sin = Math.Sin(RotorAng + Math.PI/2);
		double Cos = Math.Cos(RotorAng + Math.PI/2);
		
		GraphFunc.LineTo(PX - Cos*20, PY + Sin*20, PX + Cos*13, PY - Sin*13, 8, FinalColorWhite);
		
		RotorAng = RotorFR.Angle;
		
		PX = 275 + Math.Cos(RotorAng)*30;
		PY = 240 - Math.Sin(RotorAng)*30;
		
		Sin = Math.Sin(RotorAng + Math.PI/2);
		Cos = Math.Cos(RotorAng + Math.PI/2);
		
		GraphFunc.LineTo(275, 240, PX, PY, 3, FinalColorWhite);
		GraphFunc.LineTo(PX + Cos*13, PY - Sin*13, PX - Cos*20, PY + Sin*20, 8, FinalColorWhite);
	}
	else
		GraphFunc.DoText("DAMAGED", .7f, "Debug", TextAlignment.CENTER, 270, 230, FinalColorRed);
	
	//后悬
	if (RotorRA != null && RotorRB != null)
	{
		GraphFunc.LineTo(235, 310, 315, 310, 2, FinalColorGrayDark);
		
		double RotorAng = RotorRA.Angle;
		
		double PX = 245 + Math.Cos(RotorAng)*60;
		double PY = 310 + Math.Sin(RotorAng)*60;
		
		GraphFunc.LineTo(245, 310, PX, PY, 3, FinalColorGray);
		
		RotorAng = RotorRA.Angle + RotorRB.Angle;
		
		double PX2 = PX - Math.Cos(RotorAng)*60;
		double PY2 = PY - Math.Sin(RotorAng)*60;
		
		double Sin = Math.Sin(RotorAng + Math.PI/2);
		double Cos = Math.Cos(RotorAng + Math.PI/2);
		
		GraphFunc.LineTo(PX, PY, PX2, PY2, 3, FinalColorWhite);
		GraphFunc.LineTo(PX - Cos*13, PY - Sin*13, PX + Cos*20, PY + Sin*20, 8, FinalColorWhite);
		GraphFunc.LineTo(PX2 - Cos*13, PY2 - Sin*13, PX2 + Cos*20, PY2 + Sin*20, 8, FinalColorWhite);
	}
	else
		GraphFunc.DoText("DAMAGED", .7f, "Debug", TextAlignment.CENTER, 270, 299, FinalColorRed);
	
	GraphFunc.DoTexture("DecorativeBracketLeft", 150, 280, 30, 100, FinalColorGray);
	GraphFunc.DoTexture("DecorativeBracketRight", 360, 280, 30, 100, FinalColorGray);
	
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
	float Scale = 512f/(float)Math.Min(ScrSize.X, ScrSize.Y)/2.8f;
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
	
	GraphFunc.DoText("Battery", 1f, "Debug", TextAlignment.LEFT, 135, 174, FinalColorWhite);
	
	if (Battery != null)
	{
		Color BatteryColor = ((Battery.CurrentStoredPower/Battery.MaxStoredPower) < 0.2 ? NotAllFadeRedTimed : NotAllFadeWhite);
		
		GraphFunc.DoRect(166, 240, 280, 268, BatteryColor);
		GraphFunc.DoRect(280, 247, 284, 261, BatteryColor);
		GraphFunc.DoRect(168, 242, 278, 266, Color.Black);
	
		double BateryRemainingPercent = Battery.CurrentStoredPower/Battery.MaxStoredPower;
		
		double BarEndX = 170 + BateryRemainingPercent*106;
		
		GraphFunc.DoRect(170, 244, BarEndX, 264, BatteryColor);
		
		GraphFunc.DoText($"{Math.Round(BateryRemainingPercent*100)}%", .8f, "Debug", TextAlignment.RIGHT, 345, 245, BatteryColor);
		
		if (Battery.CurrentInput > 0)
		{
			GraphFunc.DoTexture("IconEnergy", 223, 254, 20, 20, (Math.Abs(Math.Sin(BrightnessTimer)) > 0.6 ? NotAllFadeWhite : Color.Black));
			GraphFunc.DoText("Charging", .7f, "Debug", TextAlignment.LEFT, 166, 285, FinalColorWhite);
		}
		else
			GraphFunc.DoText("Time\nRemaining", .6f, "Debug", TextAlignment.LEFT, 166, 277, FinalColorWhite);
			
		
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
		
		GraphFunc.DoText(TimeStr, .8f, "Debug", TextAlignment.RIGHT, 345, 285, FinalColorWhite);
	}
	else
	{
		GraphFunc.DoText("NO BATTERY", .8f, "Debug", TextAlignment.CENTER, 255, 255, FinalColorRed);
	}
	
	GraphFunc.DoTexture("DecorativeBracketLeft", 150, 280, 30, 100, FinalColorGray);
	GraphFunc.DoTexture("DecorativeBracketRight", 360, 280, 30, 100, FinalColorGray);
	
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
		ClearScreen(CockpitSurfaces.GetSurface(2));
		//CiLogo(CockpitSurfaces.GetSurface(1), ((float)Math.Max((6 - UnderControlTimer)/6f*100f, 0)));
		ClearScreen(CockpitSurfaces.GetSurface(1));
	}
	
	float Brightness = (float)Math.Max((UnderControlTimer - 6)/6.0*100, 0);
	
	DrawBatteryScreen(CockpitSurfaces.GetSurface(0), Brightness);
	
	if (UnderControlTimer >= 6)
	{
		DrawMainScreen(CockpitSurfaces.GetSurface(1), Brightness);
		DrawSuspScreen(CockpitSurfaces.GetSurface(2), Brightness);
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

//处理主要信息
void ProcessMain()
{
	
	Cockpit = GridTerminalSystem.GetBlockWithName(CockpitName) as IMyShipController;
	if (Cockpit != null)
	{
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
		}
		else
			Pitch = Roll = 0;
		
		//获取相对地面速度
		VehicleSpeed = Vector3D.TransformNormal(Cockpit.GetShipVelocities().LinearVelocity, RefLookAtMatrix);
		RawSpeed = -VehicleSpeed.Z;
		Speed = (float)Math.Abs(VehicleSpeed.Z);
		CockpitSpeed = (float)Cockpit.GetShipSpeed();
		
		//车身稳定
		SetGyros(PitchPID.GetPID(-Pitch*10),RollPID.GetPID(Roll*10), YawPID.GetPID(VehicleSpeed.X*RawSpeed));
		
		if (!IsUnderControl && CockpitSpeed < 2)
			Standby = true;
		
		if (RawSpeed < 0 && MoveIndicator.Z > 0)
			IsReverse = true;
	
		if ((RawSpeed > 0 && MoveIndicator.Z < 0 && CockpitSpeed*3.6 < MaxRevSpeed*1.5) || (IsHandbrake && CockpitSpeed < 2))
			IsReverse = false;
		
		if (!Standby)
		{
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
			
			if (MoveIndicator.Z != 0)
				PowerFac = (float)Math.Max(Math.Min(PowerFac - (PowerFac - MoveIndicator.Z*2)/PowerSmoothing, 1), -1);
			else
				PowerFac = 0;
		
			//动态限速以尽量防止车轮加速时打滑
			CurMaxSpd = MoveIndicator.Z > 0 ? MaxRevSpeed : MaxSpeed[Gear];
			float RealMaxSpd = (float)Math.Min(Speed*3.6*1.096+20, CurMaxSpd*1.096);
			
			//刹车
			IsHandbrake = Cockpit.GetValue<bool>("HandBrake");
			bool IsBrake = (MoveIndicator.Y > 0)|IsHandbrake|(RawSpeed*3.6f > CurMaxSpd*(RawSpeed*3.6f > 100f ? 1.01f : 1f))|(RawSpeed*3.6f < -MaxRevSpeed);
			SetBrake(IsBrake);
			
			//前进/后退
			float Power = (1f/((Speed/MaxSpeed[MaxSpeed.Length-1]*3.6f)*100f+75f)*75f);
			float Steer = (float)GetSteerAngle(Speed)*MoveIndicator.X;
			
			SetWheels(Power*PowerFac, RealMaxSpd, Steer, (float)RawSpeed, IsBrake);
			
			if (IsReverse)
				RotorSetAng(CamRotor, Steer/180*Math.PI*(0.25+CockpitSpeed/7));
			else
				RotorSetAng(CamRotor, 0);
		}
		else
		{
			Gear = 0;
			PowerFac = 0;
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
				Cockpit.ApplyAction("HandBrake");
			
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
		Echo($"Refresh in {RefreshTime - (RefreshTimer++)/60} seconds.");
	}
	Echo($"Instruction count: {Runtime.CurrentInstructionCount.ToString()} / {Runtime.MaxInstructionCount.ToString()}\nExcute time : {Math.Round(ExcuteTime = Math.Max(ExcuteTime - (ExcuteTime - Runtime.LastRunTimeMs)/20, Runtime.LastRunTimeMs), 4)}ms");
}
//VehicleController.cs by ciguisuangai
//20-02-06

const string CockpitName = "Cockpit";	//驾驶舱名
const string CockpitNameSec = "CockpitFO";	//副驾驶名
const string WheelGroupName = "Wheel";	//车轮分组名
const string SuspGroupName = "Susp";	//悬架分组名
const string BrakeGroupName = "Brake";	//刹车用远程控制分组名
const string IndicatorName = "Indicator";	//仪表盘名
float[] MaxSpeed = new float[6]{20f, 40f, 70f, 100f, 140f, 180f};	//各档位最大车速
const float MaxRevSpeed = 20f;	//最大倒车速度
const float GearUpMinFac = 0.85f;

const int RefreshTime = 10;	//刷新间隔时间（单位：秒）

//速度转向角曲线设定	（速度(m/s), 角度）
double[,] SteerCurve = new double[3,2]{{8, 25}, {12, 12.5}, {25, 4}};

List<IMyMotorSuspension> Wheels = new List<IMyMotorSuspension>();
List<IMyMotorSuspension> Susps = new List<IMyMotorSuspension>();
List<IMyShipController> Brakes = new List<IMyShipController>();
List<IMyGyro> Gyros = new List<IMyGyro>();
IMyMotorStator RotorFL, RotorFR, DoorRotor;
IMyShipController Cockpit, CockpitMain, CockpitSec;
IMyTextPanel Indicator;

int Gear = 0;
double RawSpeed;
double VertSpeed;
float Speed;
Vector3 MoveIndicator;
Vector2 RotationIndicator;
float RollIndicator;
bool IsUnderControl;
bool IsHandbrake;
bool GearDownReady = false;
bool KeyDown = false;
bool UseSecondCockpit = false;

int RefreshTimer = 0;
double ExcuteTime = 0;

//获取所需方块
void GetBlocks() {
	
	Wheels.Clear();
	Susps.Clear();
	Brakes.Clear();
	
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
	if (BlockGroup != null) {
		AllControllers.Clear();
		BlockGroup.GetBlocksOfType<IMyShipController>(AllControllers);
		foreach (IMyShipController Controller in AllControllers)
			if (Controller is IMyRemoteControl)
				Brakes.Add(Controller);
	}
	
	//获取驾驶舱
	CockpitMain = GridTerminalSystem.GetBlockWithName(CockpitName) as IMyShipController;
	CockpitSec = GridTerminalSystem.GetBlockWithName(CockpitNameSec) as IMyShipController;
	
	//获取陀螺仪
	GridTerminalSystem.GetBlocksOfType<IMyGyro>(Gyros);
	if (CockpitMain != null) InitGyros();
	
	//获取屏幕
	Indicator = GridTerminalSystem.GetBlockWithName(IndicatorName) as IMyTextPanel;
	
	//Custom part
	RotorFL = GridTerminalSystem.GetBlockWithName("rotor_susp_l") as IMyMotorStator;
	RotorFR = GridTerminalSystem.GetBlockWithName("rotor_susp_r") as IMyMotorStator;
	DoorRotor = GridTerminalSystem.GetBlockWithName("rotor_door") as IMyMotorStator;
}

//设定车轮参数
void SetWheels(string Variable, float Value) {
	foreach (IMyMotorSuspension Wheel in Wheels)
		Wheel?.SetValue<float>(Variable, Value);
}

//设定阻尼
void SetDamping(float Damping) {
	foreach (IMyMotorSuspension Susp in Susps)
		Susp?.SetValue<float>("Friction", Damping);
}

//刹车
void SetBrake(bool IsBrake) {
	foreach (IMyShipController Brake in Brakes)
		if (Brake != null) {
			var Handbrake = Brake.HandBrake;
			while (Handbrake != IsBrake) {
				Brake.ApplyAction("HandBrake");
				Handbrake = Brake.HandBrake;
			}
		}
}

//处理转向角（单位：m/s）
double GetSteerAngle(double Speed) {
	int CurveLeng = SteerCurve.Length/2;
	int i = 0;
	if (Speed < SteerCurve[0,0]) return SteerCurve[0,1];
	while (i < CurveLeng - 1) {
		if (Speed < SteerCurve[i+1,0]) {
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

//处理仪表
void ProcessIndicator(double Speed, double MaxSpd, bool HandBrake, double MoveIndicatorZ) {
	if (Indicator != null) {
		string GearTxt = (HandBrake ? "N" : (MoveIndicatorZ > 0 ? "R" : $"{Gear+1}")) + (((Speed*3.6 > MaxSpd*GearUpMinFac) && !HandBrake && (Gear != MaxSpeed.Length - 1)) ? " [C]" : (HandBrake ? " (Handbrake)" : ""));
		
		int i = 0;
		string LcdOutStr = "";
		int MaxLoops = Convert.ToInt16(Math.Floor(Math.Min(Math.Abs(Speed)*3.6, MaxSpd))/MaxSpd*37);
		while (i < MaxLoops) {
			LcdOutStr += (i == MaxLoops-1 ? " " : "|");
			i ++ ;
		}
		LcdOutStr += $"{Math.Floor(Math.Abs(Speed)*3.6)}\nGear: {GearTxt}";
		
		Indicator.ContentType = ContentType.TEXT_AND_IMAGE;
		Indicator.FontSize = 2f;
		Indicator.WriteText(LcdOutStr);
	}
}

//MEA Ship类陀螺仪部分
List<string> GyroYawField = new List<string>();
List<string> GyroPitchField = new List<string>();
List<string> GyroRollField = new List<string>();
List<float> GyroYawFactor = new List<float>();
List<float> GyroPitchFactor = new List<float>();
List<float> GyroRollFactor = new List<float>();

void InitGyros() {
	
	GyroPitchField.Clear();
	GyroRollField.Clear();
	GyroYawField.Clear();
	GyroPitchFactor.Clear();
	GyroRollFactor.Clear();
	GyroYawFactor.Clear();
	
	foreach (IMyGyro Gyro in Gyros) {
		Base6Directions.Direction GyroUp = Gyro.WorldMatrix.GetClosestDirection(CockpitMain.WorldMatrix.Up);   
		Base6Directions.Direction GyroLeft = Gyro.WorldMatrix.GetClosestDirection(CockpitMain.WorldMatrix.Left);   
		Base6Directions.Direction GyroForward = Gyro.WorldMatrix.GetClosestDirection(CockpitMain.WorldMatrix.Forward);   
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
		switch (GyroUp) {
			case Base6Directions.Direction.Up: GyroYawField.Add("Yaw"); GyroYawFactor.Add(1f); break;   
			case Base6Directions.Direction.Down: GyroYawField.Add("Yaw"); GyroYawFactor.Add(-1f); break;   
			case Base6Directions.Direction.Left: GyroYawField.Add("Pitch"); GyroYawFactor.Add(1f); break;   
			case Base6Directions.Direction.Right: GyroYawField.Add("Pitch"); GyroYawFactor.Add(-1f); break;   
			case Base6Directions.Direction.Forward: GyroYawField.Add("Roll"); GyroYawFactor.Add(-1f); break;   
			case Base6Directions.Direction.Backward: GyroYawField.Add("Roll"); GyroYawFactor.Add(1f); break;   
		}
	}
}

void SetGyros(double Pitch, double Roll, double Yaw) {
	int i = 0;
    foreach (IMyGyro Gyro in Gyros) {
		Gyro.SetValue<bool>("Override", true);
		Gyro.SetValue<float>(GyroPitchField[i], (float)Pitch*GyroPitchFactor[i]);
		Gyro.SetValue<float>(GyroRollField[i], (float)Roll*GyroRollFactor[i]);
		Gyro.SetValue<float>(GyroYawField[i], (float)Yaw*GyroYawFactor[i]);
		i ++ ;
    }
}

//处理玩家输入
void ProcessControls() {
	
	if (CockpitMain != null)
		CockpitMain.IsMainCockpit = !UseSecondCockpit;
	
	if (CockpitSec != null)
		CockpitSec.IsMainCockpit = UseSecondCockpit;
	
	Cockpit = ((UseSecondCockpit && (CockpitSec != null)) ? CockpitSec : CockpitMain);
	
	if (Cockpit != null) {
		//获取玩家输入
		MoveIndicator = Cockpit.MoveIndicator;
		RotationIndicator = Cockpit.RotationIndicator;
		RollIndicator = Cockpit.RollIndicator;
		IsUnderControl = Cockpit.IsUnderControl;
		
		//获取车速
		MatrixD RefLookAtMatrix = MatrixD.CreateLookAt(Vector3D.Zero, Cockpit.WorldMatrix.Forward, Cockpit.WorldMatrix.Up);
		Vector3D VehicleSpeed = Vector3D.TransformNormal(Cockpit.GetShipVelocities().LinearVelocity, RefLookAtMatrix);
		Vector3D MeToGround = Vector3D.Normalize(Vector3D.TransformNormal(Cockpit.GetNaturalGravity(), RefLookAtMatrix));
		RawSpeed = -VehicleSpeed.Z;
		Speed = (float)Math.Abs(VehicleSpeed.Z);
		
		//变速
		if (MoveIndicator.Y < 0) {
			if (!KeyDown) {
				if ((RawSpeed*3.6 > MaxSpeed[Gear]*GearUpMinFac) && (Gear < MaxSpeed.Length-1)) {
					Gear ++ ;
					GearDownReady = false;
				}
			}
			KeyDown = true;
		} else KeyDown = false;
		
		if (Gear > 0) {
			if (RawSpeed*3.6f > MaxSpeed[Gear-1] + 5f)
				GearDownReady = true;
			
			if (RawSpeed*3.6f < MaxSpeed[Gear-1]*(GearDownReady ? 0.98f : GearUpMinFac) | ((MoveIndicator.Y > 0) && !GearDownReady))
				Gear -- ;
		}
			
		//动态限速以尽量防止车轮加速时打滑
		float CurMaxSpd = MoveIndicator.Z > 0 ? MaxRevSpeed : MaxSpeed[Gear];
		float RealMaxSpd = (float)Math.Min(Speed*3.6*1.096+20, CurMaxSpd*1.096);
		SetWheels("Speed Limit", RealMaxSpd);
		
		//前进/后退
		float Power = (1f/((Speed/MaxSpeed[MaxSpeed.Length-1]*3.6f)*100f+75f)*75f);
		SetWheels("Propulsion override", Power*MoveIndicator.Z);
		
		//转向
		SetWheels("MaxSteerAngle", (float)GetSteerAngle(Speed));
		SetWheels("Steer override", MoveIndicator.X);
		
		//刹车
		IsHandbrake = Cockpit.GetValue<bool>("HandBrake");
		SetBrake((MoveIndicator.Y > 0)|IsHandbrake|(RawSpeed*3.6f > CurMaxSpd)|(RawSpeed*3.6f < -MaxRevSpeed));
		
		//阻尼
		SetDamping((float)Math.Max(Math.Min(Speed-12, 20), 0));
		
		//陀螺仪
		SetGyros(-MeToGround.Z*10f, MeToGround.X*10f, 0);
		
		//屏幕
		ProcessIndicator(RawSpeed, CurMaxSpd, IsHandbrake, MoveIndicator.Z);
		
		//Custom part
		if (Speed > 5f && IsUnderControl) DoorRotor?.SetValue<float>("Velocity", 5f);
		
		VertSpeed = VehicleSpeed.Y;
	}
}

public Program() {
	Runtime.UpdateFrequency = UpdateFrequency.Update1;
	GetBlocks();
	
	//Custom part
	GetSuspRotor();
	GetLights();
}

public void Main(string ArgStack, UpdateType TimerInterrput) {
	
	switch (ArgStack) {
		case "SwitchCockpit":
			UseSecondCockpit = !UseSecondCockpit;
			break;
	}
	
	if ((TimerInterrput & UpdateType.Update1) != 0) {
		ProcessControls();
		
		//Custom part
		ProcessSusp();
		ProcessLights();
		
		if (RefreshTimer > RefreshTime*60) {
			RefreshTimer = 0;
			GetBlocks();
			
			//Custom part
			GetSuspRotor();
			GetLights();
		}
		Echo($"Refresh in {RefreshTime - (RefreshTimer++)/60} seconds.");
	}
	Echo($"Instruction count: {Runtime.CurrentInstructionCount.ToString()} / {Runtime.MaxInstructionCount.ToString()}\nExcute time : {Math.Round(ExcuteTime = Math.Max(ExcuteTime - (ExcuteTime - Runtime.LastRunTimeMs)/20, Runtime.LastRunTimeMs), 4)}ms");
}

//Custom part
/*
//处理悬架转子角度
void ProcessSusp() {
	if (RotorFR != null && RotorFL != null) {
		float RotorFR_Ang = MathHelper.ToDegrees(RotorFR.Angle);
		RotorFL.UpperLimitDeg = -RotorFR_Ang - RotorFR.LowerLimitDeg;
		RotorFL.LowerLimitDeg = -RotorFR_Ang - RotorFR.UpperLimitDeg;
	}
}
*/

//落地强度强化
string[] RotorNameList = new string[4]{"rotor_susp_l", "rotor_susp_r", "rotor_susp_ra", "rotor_susp_rb"};
float[] RotorTargetAngle = new float[4]{0.2117026f, -0.1005537f, 0.1065459f, -0.115258f};
float[] RotorUpperLim = new float[4]{30f, 10f, 15f, 15f};
float[] RotorLowerLim = new float[4]{-20f, -15f, -15f, -15f};
IMyMotorStator[] SuspRotors = new IMyMotorStator[4];

void GetSuspRotor() {
	int i = 0;
	while (i < 4) {
		SuspRotors[i] = GridTerminalSystem.GetBlockWithName(RotorNameList[i]) as IMyMotorStator;
		i ++ ;
	}
}

float CalcRotorSpeed(float CurrentAngle, float TargetAngle, float Scale) {
	if (Math.Abs(CurrentAngle - TargetAngle) > 180f) 
		return -(CurrentAngle-TargetAngle+(CurrentAngle > TargetAngle ? -360f : 360f))/Scale;
	return -(CurrentAngle-TargetAngle)/Scale;
}

void ProcessSusp() {
	int i = 0;
	while (i < 4) {
		if (VertSpeed < -7) {
			SuspRotors[i].Enabled = true;
			SuspRotors[i].SetValue<float>("Velocity", (float)(10*Math.Sign(CalcRotorSpeed(MathHelper.ToDegrees(SuspRotors[i].Angle), MathHelper.ToDegrees(RotorTargetAngle[i]), .95f))));
			SuspRotors[i].UpperLimitDeg = MathHelper.ToDegrees(RotorTargetAngle[i]);
			SuspRotors[i].LowerLimitDeg = MathHelper.ToDegrees(RotorTargetAngle[i]);
			SetWheels("Strength", 100f);
		} else {
			SuspRotors[i].Enabled = false;
			SuspRotors[i].UpperLimitDeg = RotorUpperLim[i];
			SuspRotors[i].LowerLimitDeg = RotorLowerLim[i];
			SetWheels("Strength", 25f);
		}
		i ++ ;
	}
}

//获取车灯
List<IMyLightingBlock> Lights = new List<IMyLightingBlock>();
const string LightGroupName = "Lights";
int RevTimer = 0;
int SteerTimer = 0;

void GetLights() {
	Lights.Clear();
	
	var BlockGroup = GridTerminalSystem.GetBlockGroupWithName(LightGroupName);
	
	if (BlockGroup != null)
		BlockGroup.GetBlocksOfType<IMyLightingBlock>(Lights);
}

void ProcessLights() {
//	bool HeadlightOn = false;
	if (Cockpit != null) {
		if ((RawSpeed < 1) && (MoveIndicator.Z > 0) && (!IsHandbrake))
			RevTimer = 180;
		else if (RawSpeed > 1)
			RevTimer = 0;
			
		if (RevTimer > 0)
			RevTimer -- ;
		
		if (MoveIndicator.X != 0)
			SteerTimer ++ ;
		else
			SteerTimer = 0;
		
		if (SteerTimer > 48)
			SteerTimer = 0;
		
		foreach (IMyLightingBlock Light in Lights) {
			if (Light.CustomName.Contains("[L]"))
				Light.Enabled = (MoveIndicator.X < 0) && (SteerTimer < 24);
			else if (Light.CustomName.Contains("[R]"))
				Light.Enabled = (MoveIndicator.X > 0) && (SteerTimer < 24);
			
			if (Light.CustomName.Contains("[Brake]"))
				Light.Enabled = (MoveIndicator.Y > 0);
			
		//	if (Light.Contains("[Headlight]") && Light.Enabled)
		//		HeadlightOn = true
			
			if (Light.CustomName.Contains("[Back]"))
				Light.Enabled = (RevTimer > 0);
		}
	}
}
//vectorthrust.cs by ciguisuangai
//github.com/ciguisuangai
//20-01-25
//20-01-29

//默认设定：
//将左右侧转子放入VecRotors分组
//推进器放入VecThr分组
//驾驶舱命名为Cockpit

//可在自定义数据编辑设置

//Work in progress

//--Configure in custom data.--

string ControllerName;
string RotorGroupName;
string ThrusterGroupName;
string DebugLCDName;

//--------------------

List<IMyTerminalBlock> AllBlocks = new List<IMyTerminalBlock>();
List<IMyThrust> Thrusters = new List<IMyThrust>();
List<IMyMotorStator> Rotors = new List<IMyMotorStator>();

List<float> RotorThrust = new List<float>();
List<float> RotorThrustRemaining = new List<float>();
List<float> RotorThrustMax = new List<float>();
float MaxThrustTotal = 0;

List<IMyGyro> Gyros = new List<IMyGyro>();
IMyShipController Controller;

//--------------------

double ShipMass, GravityLen;
double Altitude = 0;
double AltitudeOld = 0;

//--------------------

MyIni Ini = new MyIni();
List<string> IniSections = new List<string>();
bool HadSetted = false;
bool Initalized = false;
string InitInfo = "";
int OutputCount = 0;

//--------------------

int RefreshTimer = 300;
double RunTime = 0;

//-------------- Whiplash141's vector functions ------------

Vector3D VectorProjection(Vector3D a, Vector3D b) {		//proj a on b    
	Vector3D projection = a.Dot(b) / b.LengthSquared() * b;
	return projection;
}

int VectorCompareDirection(Vector3D a, Vector3D b) {	//returns -1 if vectors return negative dot product 
	double check = a.Dot(b);
	if (check < 0)
		return -1;
	else
		return 1;
}

public static Vector3D Rejection(Vector3D a, Vector3D b) {	//reject a on b
	if (Vector3D.IsZero(a) || Vector3D.IsZero(b))
		return Vector3D.Zero;

	return a - a.Dot(b) / b.LengthSquared() * b;
}

double VectorAngleBetween(Vector3D a, Vector3D b) {		 //returns radians 
    if (a.LengthSquared() == 0 || b.LengthSquared() == 0)
        return 0;
    else
        return Math.Acos(MathHelper.Clamp(a.Dot(b) / a.Length() / b.Length(), -1, 1));
}

//-----------------------------------------------------

public void DebugOut(string Info) {
	if (DebugLCDName != "") {
		Echo(Info);
		var LCD = GridTerminalSystem.GetBlockWithName(DebugLCDName) as IMyTextPanel;
		if (LCD != null) {
			LCD.ContentType = ContentType.TEXT_AND_IMAGE;
			LCD.WriteText($"{Info}\n", (OutputCount != 0));
		} else if (OutputCount == 0) Echo($"DebugOut: {DebugLCDName} is not exist.");
		OutputCount ++ ;
	}
}

public void InitSettings() {
	Ini.Clear();
	
	Ini.Set("Vector Thrust Settings", "ControllerName", "Cockpit");
	Ini.Set("Vector Thrust Settings", "RotorGroupName", "VecRotors");
	Ini.Set("Vector Thrust Settings", "ThrusterGroupName", "VecThr");
	Ini.Set("Vector Thrust Settings", "DebugLCDName", "");
	Ini.SetComment("Vector Thrust Settings", "DebugLCDName", "If DebugLCDName is empty then no debug info output.");
	
	Me.CustomData = $"{Ini}";
}

public void LoadSettings() {
	
	Ini.Clear();
	Ini.TryParse(Me.CustomData);
	
	IniSections.Clear();
	Ini.GetSections(IniSections);
	
	foreach (string Section in IniSections) {
		if (Section == "Vector Thrust Settings") HadSetted = true;
	}
	
	if (!HadSetted) {
		InitSettings();
	}
	
	Ini.Clear();
	Ini.TryParse(Me.CustomData);
	ControllerName = Ini.Get("Vector Thrust Settings", "ControllerName").ToString();
	RotorGroupName = Ini.Get("Vector Thrust Settings", "RotorGroupName").ToString();
	ThrusterGroupName = Ini.Get("Vector Thrust Settings", "ThrusterGroupName").ToString();
	DebugLCDName = Ini.Get("Vector Thrust Settings", "DebugLCDName").ToString();
}

public void GetBlocks() {
	Gyros.Clear();
	IMyBlockGroup RotorGroup = GridTerminalSystem.GetBlockGroupWithName(RotorGroupName);
	IMyBlockGroup ThrusterGroup = GridTerminalSystem.GetBlockGroupWithName(ThrusterGroupName);
	
	if (RotorGroup != null && ThrusterGroup != null) {
		RotorGroup.GetBlocksOfType<IMyMotorStator>(Rotors);
		
		ThrusterGroup.GetBlocksOfType<IMyThrust>(Thrusters);
		
		GridTerminalSystem.GetBlocksOfType(AllBlocks);
		foreach (IMyTerminalBlock Block in AllBlocks) if (Block is IMyGyro) Gyros.Add(Block as IMyGyro);
		
		Initalized = true;
	}
	
	InitInfo = $"{RotorGroup != null},{ThrusterGroup != null}";
}	

public void OutputDebugInfo() {
	/*
	foreach (IMyTerminalBlock Block in AllBlocks) DebugOut($"{Block}");
	foreach (IMyGyro Block in Gyros) DebugOut($"{Block}");
	foreach (IMyThrust Block in Thrusters) DebugOut($"{Block}");
	foreach (IMyMotorStator Block in Rotors) DebugOut($"{Block}");
	*/
	
	DebugOut($"MaxThrustTotal: {MaxThrustTotal}");
	
	foreach (IMyMotorStator Block in Rotors) {
		DebugOut($"{Block.CustomName} : {Block.Orientation}, {Math.Round(MathHelper.ToDegrees(Block.Angle),4)}");
	}
}

public void SetGyros(double Pitch, double Roll, double Yaw, MatrixD RefMatrix) {
    foreach (IMyGyro Gyro in Gyros) {
		//MEA Class Ship's gyroscope processing method
		Base6Directions.Direction GyroUp = Gyro.WorldMatrix.GetClosestDirection(RefMatrix.Up);   
		Base6Directions.Direction GyroLeft = Gyro.WorldMatrix.GetClosestDirection(RefMatrix.Left);   
		Base6Directions.Direction GyroForward = Gyro.WorldMatrix.GetClosestDirection(RefMatrix.Forward);   
		float PitchFac = 1f;
		float RollFac = 1f;
		float YawFac = 1f;
		string PitchVal = "Pitch";
		string RollVal = "Roll";
		string YawVal = "Yaw";
		
		switch (GyroUp) {
			case Base6Directions.Direction.Up: YawVal = "Yaw"; YawFac = 1f; break;   
			case Base6Directions.Direction.Down: YawVal = "Yaw"; YawFac = -1f; break;   
			case Base6Directions.Direction.Left: YawVal = "Pitch"; YawFac = 1f; break;   
			case Base6Directions.Direction.Right: YawVal = "Pitch"; YawFac = -1f; break;   
			case Base6Directions.Direction.Forward: YawVal = "Roll"; YawFac = -1f; break;   
			case Base6Directions.Direction.Backward: YawVal = "Roll"; YawFac = 1f; break;   
		}  
		switch (GyroLeft) {
			case Base6Directions.Direction.Up: PitchVal = "Yaw"; PitchFac = 1f; break;   
			case Base6Directions.Direction.Down: PitchVal = "Yaw"; PitchFac = -1f; break;   
			case Base6Directions.Direction.Left: PitchVal = "Pitch"; PitchFac = 1f; break;   
			case Base6Directions.Direction.Right: PitchVal = "Pitch"; PitchFac = -1f; break;   
			case Base6Directions.Direction.Forward: PitchVal = "Roll"; PitchFac = -1f; break;   
			case Base6Directions.Direction.Backward: PitchVal = "Roll"; PitchFac = 1f; break;   
		}
		switch (GyroForward) {
			case Base6Directions.Direction.Up: RollVal = "Yaw"; RollFac = 1f; break;   
			case Base6Directions.Direction.Down: RollVal = "Yaw"; RollFac = -1f; break;   
			case Base6Directions.Direction.Left: RollVal = "Pitch"; RollFac = 1f; break;   
			case Base6Directions.Direction.Right: RollVal = "Pitch"; RollFac = -1f; break;   
			case Base6Directions.Direction.Forward: RollVal = "Roll"; RollFac = -1f; break;   
			case Base6Directions.Direction.Backward: RollVal = "Roll"; RollFac = 1f; break;   
		}
		
		Gyro.SetValue<bool>("Override", true);
        
		Gyro.SetValue<float>(PitchVal, (float)Pitch*PitchFac);
		Gyro.SetValue<float>(RollVal, (float)Roll*RollFac);
		Gyro.SetValue<float>(YawVal, (float)Yaw*YawFac);
    }
}

public float CalcRotorSpeed(float CurrentAngle, float TargetAngle, float Scale) {
	if (Math.Abs(CurrentAngle - TargetAngle) > 180f) 
		return -(CurrentAngle-TargetAngle+(CurrentAngle > TargetAngle ? -360f : 360f))/Scale;
	return -(CurrentAngle-TargetAngle)/Scale;
}

public void RotorSetAng(IMyMotorStator Rotor, double Angle) {
    if (Rotor != null) {
        Rotor.SetValue<float>("Velocity", CalcRotorSpeed(MathHelper.ToDegrees(Rotor.Angle), (float)(Angle/Math.PI*180), .95f));
    } else DebugOut($"RotorSetAng: {Rotor} is not exist.\n");
}

public void ProcessVecThrusrt() {
	if (Controller != null) {
	//	Clean up.
		MaxThrustTotal = 0;
		RotorThrust.Clear();
		RotorThrustRemaining.Clear();
		RotorThrustMax.Clear();
		
		//	Loop 1:
		//	Get total effective thrust.
		foreach (IMyThrust Thruster in Thrusters) MaxThrustTotal += Thruster.MaxEffectiveThrust;
		
		//	Loop 2:
		//	Get total effective thrust of each rotor top.
		int i = 0;
		foreach (IMyMotorStator Rotor in Rotors) {
			
			RotorThrustMax.Add(0f);
			//	Loop 2-1:
			foreach (IMyThrust Thruster in Thrusters) {
				if (Rotor.TopGrid == Thruster.CubeGrid) RotorThrustMax[i] += Thruster.MaxEffectiveThrust;
			}
			
			DebugOut($"{RotorThrustMax[i]}");
			i ++ ;
		}
		
		var RefForward = Controller.WorldMatrix.Forward;
		var RefLeft = Controller.WorldMatrix.Left;
		var RefUp = Controller.WorldMatrix.Up;
		
		MatrixD RefLookAtMatrix = MatrixD.CreateLookAt(Vector3D.Zero, RefForward, RefUp);
		
		ShipMass = Controller.CalculateShipMass().PhysicalMass;
		Vector3D Gravity = Controller.GetNaturalGravity();
		GravityLen = Vector3D.TransformNormal(-Gravity, RefLookAtMatrix).Length();
		DebugOut($"{GravityLen == 0}");

		Vector3D MeToPlane = Vector3D.Normalize(Vector3D.TransformNormal(-Controller.GetNaturalGravity(), RefLookAtMatrix));
		Vector3D ShipSpeed = Vector3D.TransformNormal(Controller.GetShipVelocities().LinearVelocity, RefLookAtMatrix);
		
		var RotationIndicator = Controller.RotationIndicator;
		var MoveIndicator = Controller.MoveIndicator;
		var RollIndicator = Controller.RollIndicator;

		//---Get Roll and Pitch Angles 
		float Pitch = (float)(Math.Acos(MathHelper.Clamp(Gravity.Dot(RefForward) / GravityLen, -1, 1)) - Math.PI / 2);

		Vector3D PlanetRelLeftVec = RefForward.Cross(Gravity);
		float Roll = (float)(VectorAngleBetween(RefLeft, PlanetRelLeftVec)*VectorCompareDirection(VectorProjection(RefLeft, Gravity), Gravity));
		
		Vector3D Forward = Controller.WorldMatrix.Forward;
		Vector3D Left = Vector3D.Cross(Forward, Gravity);
		Forward = Vector3D.Cross(Gravity, Left);
		MatrixD LookAt = MatrixD.CreateLookAt(Vector3D.Zero, Forward, -Gravity);
		
		Vector3D GroundSpeed = Vector3D.TransformNormal(Controller.GetShipVelocities().LinearVelocity, LookAt);
		
		if (GravityLen == 0) {
			GroundSpeed = ShipSpeed;
			MeToPlane = Vector3D.Zero;
		}
		
		SetGyros(MeToPlane.Z*30f-RotationIndicator.X*3f, -MeToPlane.X*60f, RotationIndicator.Y*5f, Controller.WorldMatrix);
		
		Controller.TryGetPlanetElevation(MyPlanetElevation.Sealevel, out Altitude);
		
		DebugOut($"MoveIndicator: {MoveIndicator}\nPitch: {Math.Round(MathHelper.ToDegrees(Pitch),4)},Roll: {Math.Round(MathHelper.ToDegrees(Roll),4)}\n" + 
			$"GravityLen: {Math.Round(GravityLen,4)},ShipMass: {Math.Round(ShipMass,4)}\n" + 
			$"MeToPlane: {Math.Round(MeToPlane.X,4)},{Math.Round(MeToPlane.Y,4)},{Math.Round(MeToPlane.Z,4)}\n" +
			$"ShipSpeed: {Math.Round(ShipSpeed.X,4)},{Math.Round(ShipSpeed.Y,4)},{Math.Round(ShipSpeed.Z,4)}\n" + 
			$"GroundSpeed: {Math.Round(GroundSpeed.X,4)},{Math.Round(GroundSpeed.Y,4)},{Math.Round(GroundSpeed.Z,4)}\n" + 
			$"Altitude: {Math.Round(Altitude, 4)}\nvY: {Math.Round((Altitude - AltitudeOld)/(1.0/60.0),4)}\nShipMass: {ShipMass}");
		
		float TotalLift = (GravityLen != 0 ? (float)(ShipMass*GravityLen*(MoveIndicator.Y <= 0 ? Math.Max(Math.Min(1-GroundSpeed.Y/10,1),0) : 1)) : 0f);
		DebugOut($"TotalLift: {Math.Round(TotalLift, 4)}");
		
		//	Loop 3:
		//	Calculate thrust of each rotor and set angle and thrust.
		i = 0;
		foreach (IMyMotorStator Rotor in Rotors) {
			
			float Angle = ((Rotor.Orientation.Up != Base6Directions.Direction.Right) ? (2f*(float)Math.PI - Rotor.Angle) : Rotor.Angle);
			if (Angle > Math.PI) Angle = -(float)(2*Math.PI - Angle);
			if (GravityLen != 0) Angle -= (float)Pitch;
			double TranAng = Math.Abs(Angle) > Math.PI/2 ? (Math.PI - Math.Abs(Angle)) : Math.Abs(Angle);
			DebugOut($"Angle: {Math.Round(Angle, 4)}, TranAng: {Math.Round(TranAng, 4)}");
		
			// Calculate thrust
			float Lift4EachRotor = (GravityLen != 0 ? TotalLift*(RotorThrustMax[i]/MaxThrustTotal) : 0);
			
			float ThrustRemaining = RotorThrustMax[i]-Lift4EachRotor;
			
			Vector2D FirstThrVec = new Vector2D(0, Lift4EachRotor);
			
			double VFactor = Math.Max(Math.Min(-MoveIndicator.Y+(MoveIndicator.Y == 0 ? Math.Min(GroundSpeed.Y/10,0) : 0),1),-1);
			if (GravityLen == 0) VFactor = Math.Max(Math.Min(-MoveIndicator.Y+(MoveIndicator.Y == 0 ? GroundSpeed.Y/10 : 0),1),-1);
			double FFactor = Math.Max(Math.Min(MoveIndicator.Z-(MoveIndicator.Z == 0 ? GroundSpeed.Z/60 : 0),1),-1);
		//	double VFactor = -MoveIndicator.Y;
		//	double FFactor = MoveIndicator.Z;
			DebugOut($"VFac {Math.Round(VFactor,4)},FFac {Math.Round(FFactor,4)}");
			
			double AdditionalThrust = Math.Max(Math.Min(Math.Abs(VFactor) + Math.Abs(FFactor), 1)*ThrustRemaining, 0);
			
			double FinalThrust = Math.Min(Lift4EachRotor + AdditionalThrust, RotorThrustMax[i]);
			
		/*	TestAng += Math.PI/180;
			double SecVecLengDebug = Math.Sqrt(Math.Pow(FinalThrust,2)-Math.Pow(Lift4EachRotor*Math.Cos(TestAng),2))+Lift4EachRotor*Math.Sin(TestAng);
			Vector2D SecondThrVecDebug = new Vector2D(SecVecLengDebug*Math.Cos(TestAng), -SecVecLengDebug*Math.Sin(TestAng));
	
			DebugOut($"SecVecLengDebug: {SecVecLengDebug}, {FinalThrust >= Lift4EachRotor}\n2nd Vec: {Math.Round(SecondThrVecDebug.X)},{Math.Round(SecondThrVecDebug.Y)}");*/
			
			double SecVecAngle = (FFactor == 0 ? (VFactor != 0 ? Math.PI/2*Math.Sign(VFactor) : 0) : Math.Atan(VFactor/FFactor));
			if (FFactor < 0) SecVecAngle = SecVecAngle - Math.PI;
			if (VFactor > 0) SecVecAngle = Math.PI - SecVecAngle;
			double SecVecLeng = Math.Sqrt(Math.Pow(FinalThrust,2)-Math.Pow(Lift4EachRotor*Math.Cos(SecVecAngle),2))+Lift4EachRotor*Math.Sin(SecVecAngle);
			
			Vector2D SecondThrVec = new Vector2D(SecVecLeng*Math.Cos(SecVecAngle), -SecVecLeng*Math.Sin(SecVecAngle));
			
			Vector2D FinalThrVec = FirstThrVec + SecondThrVec;
			
			double VecAng = Math.Max(Math.PI/2 - Math.Abs(Math.Atan(FinalThrVec.Y/FinalThrVec.X)), Math.PI/4);
		//	if (FinalThrVec.X < 0) VecAng = VecAng - Math.PI;
		//	if (FinalThrVec.Y > 0) VecAng = Math.PI - VecAng;
			
			DebugOut($"LFER: {Math.Round(Lift4EachRotor)}, AT: {AdditionalThrust}\n1st Vec: {Math.Round(FirstThrVec.X)},{Math.Round(FirstThrVec.Y)}\n" + 
				$"SVA: {Math.Round(SecVecAngle/(Math.PI/180), 2)}, SVL: {Math.Round(SecVecLeng)}\n" + 
				$"2nd Vec: {Math.Round(SecondThrVec.X)},{Math.Round(SecondThrVec.Y)}\n" + 
				$"1st+2nd: {Math.Round(FinalThrVec.X)},{Math.Round(FinalThrVec.Y)}\nFinLen: {Math.Round(FinalThrVec.Length())}");
				
		/*	if (MoveIndicator.Y <= 0) ForwardThrust = (float)(MoveIndicator.Z == 0 ? Math.Max(Math.Min(-GroundSpeed.Z/10, 1), -1) : MoveIndicator.Z)*(float)Math.Sqrt(Math.Max(Math.Pow(RotorThrustMax[i],2) - Math.Pow(Lift4EachRotor,2), 0));
			else if (MoveIndicator.Z != 0) {
				EqualThrust = (float)(Math.Sqrt(Math.Max(2*Math.Pow(RotorThrustMax[i],2)-Math.Pow(Lift4EachRotor,2), 0))-Lift4EachRotor)/2f;
				ForwardThrust = MoveIndicator.Z*EqualThrust;
				VertThrust += EqualThrust;
			} else VertThrust = -MoveIndicator.Y*RotorThrustMax[i];*/
			
			
			float TargetThrust = (float)(TranAng < Math.PI/4 ? (FinalThrVec.Y/Math.Cos(Angle)) : Math.Abs((FinalThrVec.X/Math.Sin(Angle))));
			
			double TargetThrustAngle = (GravityLen != 0 ? Pitch : 0) - (FinalThrVec.Y == 0 ? (FFactor != 0 ? Math.PI/2*Math.Sign(FinalThrVec.X) : 0) : Math.Atan(FinalThrVec.X/FinalThrVec.Y));
			if (FinalThrVec.Y < 0) TargetThrustAngle = Math.PI - TargetThrustAngle;
			
		//	float TargetThrustAngle = Pitch - 
		//		(float)Math.Acos(Math.Min(Lift4EachRotor/RotorThrustMax[i], 1))*(MoveIndicator.Z != 0 ? MoveIndicator.Z : (float)Math.Min(Math.Max(-GroundSpeed.Z/10, -1),1));
		//	DebugOut($"Forward: {Math.Round(ForwardThr)}, Vert: {Math.Round(VertThrust)}, Equal: {Math.Round(EqualThrust)}\nTT: {Math.Round(TargetThrust)}, TTA: {Math.Round(TargetThrustAngle, 4)}");
		//	RotorThrust.Add(TargetThrust);
			DebugOut($"VA: {Math.Round(VecAng,4)}, TT: {TargetThrust}, TTA: {TargetThrustAngle}");
			
			//	Loop 3-1:
			foreach (IMyThrust Thruster in Thrusters) 
				if (Rotor.TopGrid == Thruster.CubeGrid) {
					Thruster.ThrustOverridePercentage = (Thruster.MaxEffectiveThrust*(TargetThrust/RotorThrustMax[i]))/Thruster.MaxEffectiveThrust;
					Thruster.ApplyAction(Thruster.ThrustOverridePercentage == 0f ? "OnOff_Off" : "OnOff_On");
					DebugOut($"{Thruster.ThrustOverridePercentage}");
				}
			
			RotorSetAng(Rotor, (Rotor.Orientation.Up == Base6Directions.Direction.Right ? 1f : -1f)*(float)TargetThrustAngle);
			
			i ++ ;
		}
		var Ang = Rotors[0].Angle;
		
		AltitudeOld = Altitude;
			
	} else Controller = GridTerminalSystem.GetBlockWithName(ControllerName) as IMyShipController;
}

public Program() {
	Runtime.UpdateFrequency = UpdateFrequency.Update1;
	LoadSettings();
	GetBlocks();
}

public void Main(string Argument, UpdateType UpdateSource) {
	OutputCount = 0;
	
	string EchoStr = $"-- Vector thruster --\nby ciguisuangai\n\nEdit settings in custom data.\nRefresh in {(RefreshTimer--)/60} seconds.\n\nExcute time : {Math.Round(RunTime = Math.Max(RunTime - (RunTime - Runtime.LastRunTimeMs)/20, Runtime.LastRunTimeMs), 4)}ms";
	if (DebugLCDName != "") DebugOut(EchoStr);
	else Echo(EchoStr);
	
	if (RefreshTimer == 0) {
		RefreshTimer = 300;
		GetBlocks();
	}
	DebugOut($"ControllerName: {ControllerName}\nRotorGroupName: {RotorGroupName}\nThrusterGroupName: {ThrusterGroupName}\nInitalized: {Initalized}\n{InitInfo}\n");
	if ((UpdateSource & UpdateType.Update1) != 0) {
		ProcessVecThrusrt();
		OutputDebugInfo();
	}
	
	Echo($"\nInstruction Count : {Runtime.CurrentInstructionCount} / {Runtime.MaxInstructionCount}");
}
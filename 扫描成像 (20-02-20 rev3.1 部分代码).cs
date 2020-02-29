//扫描成像 by ciguisuangai (20-02-20 rev3.1)
//（部分代码 20-02-29）
//https://github.com/ciguisuangai/SEScript_Pub

//----------------------------------------

//Settings -------------------------------

//运行命令 Run （区分大小写）开始扫描
//运行命令 Stop 停止扫描

const string CamName = "Cam";		//摄像机名称
const string LCDName = "LCD";		//屏幕名称
const UInt16 InitWidth = 256;		//分辨率宽度（最大512，文字模式最大256）
const UInt16 InitHeight = 256;		//分辨率高度（最大512，文字模式最大256）
const float ScanDist = 200f;		//扫描距离
const float Scale = 1f;				//放大倍数
//[Deleted]
const double MaxBrightness = 128;	//最大亮度
const double DeltaScaleP = 1.6;		//正斜率除数，越低物体边缘越明显
const double DeltaScaleN = 11.5;	//负斜率除数，越低上下表面亮度差越明显
bool UsingDeltaMode = true;			//是否使用斜率模式（仅图形模式）
bool LoopingMode = false;			//循环模式（摄像机），运行命令 Stop 停止。建议开启此模式时将分辨率设置在64*64以内，并且Scale = 1f

//General --------------------------------

const Int64 MaskBits48 = 0xFFFFFFFFFFFF;

IMyCameraBlock Camera;
IMyTextSurface Panel;

MatrixD RefLookAtMatrix;
Vector3D CamPos;
UInt16 Width, Height, PosX, PosY;
float RealScale;

UInt32 OutputPtr;

UInt16 Cycles = 0;
byte Step = 0;

bool Started;
bool AllFin;
bool LoopFin;

//Initalize ------------------------------

//rgb function from https://steamcommunity.com/sharedfiles/filedetails/?id=787881521
static char rgb(byte r, byte g, byte b)
{
	return (char)(0xe100 + (r << 6) + (g << 3) + b);
}

void GetBlocks()
{
	Camera = GridTerminalSystem.GetBlockWithName(CamName) as IMyCameraBlock;
    Panel = GridTerminalSystem.GetBlockWithName(LCDName) as IMyTextSurface;
}

void InitGeneral()
{
	GetBlocks();
	
	Cycles = Step = 0;
	
	Started = AllFin = ScanInited = OutputInited = false;
	
	LoopFin = true;
}

//Scan -----------------------------------

Int64[] DistTable;

bool ScanInited;

void InitScan()
{
	if (!ScanInited)
	{
		if (Camera == null)
		{
			AllFin = true;
			return;
		}
		
		//----------------------------------------------- [85 - 106][Deleted]
	}
}

void Scan()
{
		//---------------------------------------------- [112 - 174][Deleted]
}

//Output ---------------------------------

MySpriteDrawFrame FrameBuffer;
MySprite Pixel;
float PixelSize;

bool OutputInited;

Color[] TagColorTable = new Color[7]
{
	Color.White,				//No tag
	new Color(180, 255, 180),	//Grid friendly
	new Color(0, 200, 255),		//Grid
	new Color(255, 127,	127),	//Grid enemy
	new Color(100, 255, 100),	//Character friendly
	new Color(255, 200, 0),		//Character enemy
	new Color(255, 0, 255)		//Floating object
};

void InitOutput()
{
	if (!OutputInited)
	{
		if (Panel == null)
		{
			AllFin = true;
			return;
		}
		
		LoopFin = false;
		
		OutputPtr = PosX = PosY = 0;
		
		FrameBuffer = Panel.DrawFrame();
			
		PixelSize = 512f/Width;
		Pixel = MySprite.CreateSprite("SquareSimple", Vector2.Zero, new Vector2(PixelSize, PixelSize));
			
		Panel.ContentType = ContentType.SCRIPT;
		Panel.ScriptBackgroundColor = Color.Black;
		Panel.Script = "";
		
		OutputInited = true;
	}
}

void OutputResult() {
	if (OutputInited)
	{
		Pixel.Position = new Vector2(PosX*PixelSize,PosY*PixelSize);
			
		Int32 Ptr = PosY*Width + PosX;
			
		double Brightness = 0;
			
		if (!UsingDeltaMode)
			Brightness = ((8192.0/(((DistTable[Ptr] & MaskBits48) + 32)/512.0))/65536.0)*MaxBrightness;
		else
		{
			if (PosX > 0 && PosY > 0 && PosX < Width - 1 && PosY < Height - 1)
			{	
				double DeltaL = 0;
				double DeltaR = 0;
					
				DeltaL = ((DistTable[Ptr - 1] & MaskBits48) + (DistTable[Ptr - Width] & MaskBits48))/2048.0 - (DistTable[Ptr] & MaskBits48)/1024.0;
					
				DeltaR = ((DistTable[Ptr + 1] & MaskBits48) + (DistTable[Ptr + Width] & MaskBits48))/2048.0 - (DistTable[Ptr] & MaskBits48)/1024.0;
					
				Brightness = Math.Max(Math.Min(1, 0.5 + (DeltaL + DeltaR)/(DeltaScaleP/RealScale) + Math.Max((DeltaL - DeltaR)/(DeltaScaleN/RealScale), -0.65)), 0);
				if ((DistTable[Ptr - Width] & MaskBits48)/1024 == 8192) Brightness = 0;
					
			}
		}
			
		byte TypeBits = (byte)((DistTable[Ptr] & 0xFF000000000000) >> 48);
			
		byte TypeBitsLow = (byte)(TypeBits & 0x07);		//Type
		byte TypeBitsHigh = (byte)(TypeBits & 0x38);	//Relationship
			
		byte TypeNum = 0;
			
		if (TypeBitsHigh == 0x08)
			switch (TypeBitsLow)
			{
				case 0x01:
					TypeNum = 1;
					break;
				case 0x02:
					TypeNum = 4;
					break;
			}
		else if (TypeBitsHigh == 0x10)
			switch (TypeBitsLow)
			{
				case 0x01:
					TypeNum = 3;
					break;
				case 0x02:
					TypeNum = 5;
					break;
			}
		else
		{
			switch (TypeBitsLow)
			{
				case 0x01:
					TypeNum = 2;
					break;
				case 0x04:
					TypeNum = 6;
					break;
			}
		}
			
		Pixel.Color = new Color((int)(TagColorTable[TypeNum].R*Brightness), (int)(TagColorTable[TypeNum].G*Brightness), (int)(TagColorTable[TypeNum].B*Brightness));
			
		FrameBuffer.Add(Pixel);
		
		PosX ++ ;
		
		if (PosX == Width)
		{
			PosY ++ ;
			PosX = 0;
			
			if (PosY == Height)
			{
				FrameBuffer.Dispose();
				AllFin = LoopFin = true;
			}
		}
	}
}

//General --------------------------------

void GeneralLoop() {
	//[Deleted]
		if (LoopFin)
		{
			switch (Step)
			{
				case 0:
					InitScan();
					break;
				case 1:
					InitOutput();
					break;
			}
			Step ++ ;
		}
		else
		{
			switch (Step)
			{
				case 1:
					Scan();
					break;
				case 2:
					OutputResult();
					break;
			}
		}
	}
	
	//[Deleted]
	//[Deleted]
}

public Program()
{
	Runtime.UpdateFrequency = UpdateFrequency.Update1;
	
	InitGeneral();
}

public void Main(string Argument, UpdateType UpdateSource)
{
	switch (Argument)
	{
        case "Run":
			if (!Started || AllFin)
			{
				InitGeneral();
				Started = true;
			}
            break;
		case "Stop":
			InitGeneral();
			break;
	}
	
	if (Started)
		GeneralLoop();
	
	if (LoopingMode && AllFin)
	{
		InitGeneral();
		Started = true;
	}
	
	Echo($"Cycles: {Cycles}\nStep: {Step}\nPX: {PosX}, PY: {PosY}\n{ScanInited}, {OutputInited}\nFinished: {AllFin}");
}
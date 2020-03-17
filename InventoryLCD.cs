/*
InventoryLCD by 次硅酸钙
20-03-17 version 0.0.1

未经允许请勿发布该脚本和其衍生版本。
允许在自己的蓝图中使用该脚本，但请说明注明脚本来源。
https://github.com/ciguisuangai/SEScript_Pub/blob/master/InventoryLCD.cs

使用说明：

在需要显示内容的方块的自定义数据第一行填写InventoryLCD，从第二行开始填写指令，指令按顺序执行。
运行指令 Refresh 进行刷新

显示内容的颜色可以通过设置屏幕的文本和背景颜色更改

若对应LCD的自定义数据会被其他脚本修改可能会造成冲突

可用指令：

	设定：

		*在执行下一条相同指令设定之前将会保持设定结果

		设定需要显示的屏幕（并重置设定）（仅适用于具有多个屏幕的方块）
			lcd,[编号]

		缩放
			scale,[数值]
	
		设定运行速度
			setspd,[数值]
	
		设定翻页时间间隔
			settime,[数值]
			
		注：为降低性能消耗此程序采用分步执行方法，需要处理的指令总数越多运行速度越慢，
			请使用settime和setspd指令调节处理速度。
	
		设定面板最大高度（仅影响item指令）
			setheight,[数值>128]
			
			若设定太低可能导致程序出错
	
		注释
			#,[内容]

	面板：

		物品
			item,[方块/分组名称],[类型1],[类型2], ...
			
			无参数或名称为 all 时显示所有方块信息
			无类型参数时默认显示所有类型
	
			可用类型：
				ore			矿石
				ingot		锭
				component	配件
				charitem	随身物品
				ammo		弹药
				misc		其他类型

		空间使用状况（一般方块）
			remain,[方块/分组名称]
			
			无参数或名称为 all 时显示所有方块信息

		空间使用状况（生产方块）
			remain2,[方块/分组名称]
			
			无参数或名称为 all 时显示所有方块信息

示例：

InventoryLCD
#,This is example
#,设定显示编号为0的lcd
lcd,0
#,设定面板最大高度为384
setheight,384
#,设定翻页时间间隔为10
settime,10
#,设定缩放为1.2
scale,1.2
#,显示所有方块库存中的矿石
item,all,ore
#,显示生产方块Refinery的库存
item,Refinery
#,设定缩放为0.8
scale,0.8
#,显示生产方块Refinery的空间使用情况
remain2,Refinery
#,显示所有方块的空间使用情况
remain
#,设定显示编号为1的lcd
lcd,1
#,显示所有库存
item

*/
		
//Plan:
//
//Using MyIni to avoid 
//
//Arguments:
//
//	scale,value
//
//	setspd,value
//
//	settime,value
//
//	setheight,value
//		affects item only
//
//	(which)
//		all
//		empty = all
//		[group name]
//		if group does not exist then [block name]
//
//	lcd,num
//		IMyTextSurfaceProvider only
//
//	item,which,type1,type2,...
//		ore
//		ingot
//		component
//		charitem
//		ammo
//		misc
//
//	remain,which
//
//	remain2,which
//		for production blocks
//
//Planning Arguments:
//	power,
//		which
//		total
//		battery
//		reactor
//		wind
//		solar
//		engine
//
//	vent,which
//
//	fuel,which
//
//	speed,unit
//		ms
//		kph
//		mph
//		kont
//
//	pos
//
//	mscroll
//
//

double ExcuteTime = 0;

class MyItem
{
	public double Amount;
	public string TypeId;
	public string SubtypeId;
	
	public MyItem(string InitTypeId, string InitSubtypeId, double InitAmount = 0)
	{
		TypeId = InitTypeId;
		SubtypeId = InitSubtypeId;
		Amount = InitAmount;
	}
	
	public string Type
	{
		get
		{
			return TypeId + "/" + SubtypeId;
		}
	}
}

class SortedItems
{
	public List<MyItem> AllItems = new List<MyItem>();
	public List<MyItem> Ore = new List<MyItem>();
	public List<MyItem> Ingot = new List<MyItem>();
	public List<MyItem> Component = new List<MyItem>();
	public List<MyItem> CharItem = new List<MyItem>();
	public List<MyItem> Ammo = new List<MyItem>();
	public List<MyItem> Misc = new List<MyItem>();
	public double TotalVol = 0;
	public double TotalMaxVol = 0;
	
	public void Cleanup()
	{
		AllItems.Clear();
		Ore.Clear();
		Ingot.Clear();
		Component.Clear();
		CharItem.Clear();
		Ammo.Clear();
		Misc.Clear();
		TotalVol = 0;
		TotalMaxVol = 0;
	}
}

class ItemSpace
{
	public double TotalVol = 0;
	public double TotalMaxVol = 0;
	
	public void Cleanup()
	{
		TotalVol = 0;
		TotalMaxVol = 0;
	}
}

static double Fix2Double(MyFixedPoint Fix)
{
	double Out = 0;
	
	var StrRaw = Fix.ToString();
	double.TryParse(StrRaw, out Out);
	
	return Out;
}

static string GetOreType(string SubtypeId)
{
	switch (SubtypeId)
	{
		case "Stone":
			return "Stone";
		case "Iron":
			return "Fe";
		case "Nickel":
			return "Ni";
		case "Cobalt":
			return "Co";
		case "Magnesium":
			return "Mg";
		case "Silicon":
			return "Si";
		case "Silver":
			return "Ag";
		case "Gold":
			return "Au";
		case "Platinum":
			return "Pt";
		case "Uranium":
			return "U";
		case "Organic":
			return "Organic";
		case "Ice":
			return "Ice";
	}
	return "";
}

SortedItems AllItemOnShip = new SortedItems();

const string ERR_ARG = "Too few arguments";
const string ERR_PARA = "Parameter error";

class MySurface
{
	public IMyTextSurface Surface;
	public List<string> Args;
	public int Time;
	public int Scroll;
	
	public MySurface()
	{
		Surface = null;
		Args = new List<string>();
		Time = Scroll = 0;
	}
}

List<MySurface> Surfaces = new List<MySurface>();

class BarInfo
{
	public string Title;
	public double Percent;
	
	public BarInfo(double InitPercent, string InitTitle = "")
	{
		Percent = InitPercent;
		Title = InitTitle;
	}
}

class MyGraphFunc
{

	public MySpriteDrawFrame drawframe;
	public float viewport;
	
	public MyGraphFunc(MySpriteDrawFrame initdrawframe, float initviewport = 0f)
	{
		drawframe = initdrawframe;
		viewport = initviewport;
	}

	#region GraphicalFunc
	public void LineTo(double x1, double y1, double x2, double y2, float thickness, Color linecolor)
	{
		if (((x1 > 512 && x2 > 512) || (x1 < 0 && x2 < 0)) && ((y1 > 512 && y2 > 512) || (y1 < 0 && y2 < 0))) return;
		float leng = (float)Math.Sqrt((x1-x2)*(x1-x2) + (y1-y2)*(y1-y2));
		float midX = (float)((x1+x2)/2);
		float midY = (float)((y1+y2)/2);
		MySprite line = MySprite.CreateSprite("SquareSimple", new Vector2(midX, midY + viewport), new Vector2(leng, thickness));
		line.RotationOrScale = (float)(Math.Sign(x1-x2)*Math.Sign(y1-y2)*Math.Atan(Math.Abs(y1-y2)/Math.Abs(x1-x2)));
		if (x1-x2 == 0) line.RotationOrScale = (float)3.1415926535897932384626433832795/2f;
		line.Color = linecolor;
		drawframe.Add(line);
	}

	public void CircleAt(double x1, double y1, float size, Color circolor)
	{
		if ((x1 > 512 || x1 < 0) && (y1 > 512+size*2 || y1 < -size*2)) return;
		MySprite circle = MySprite.CreateSprite("Circle", new Vector2((float)x1, (float)y1 + viewport), new Vector2(size, size));
		circle.Color = circolor;
		drawframe.Add(circle);
	}

	public void DoRect(double x1, double y1, double x2, double y2, Color rectcolor)
	{
		if (((x1 > 512 && x2 > 512) || (x1 < 0 && x2 < 0)) && ((y1 > 512 && y2 > 512) || (y1 < 0 && y2 < 0))) return;	
		MySprite rect = MySprite.CreateSprite("SquareSimple", new Vector2((float)(x1+(x2-x1)/2), (float)(y1+(y2-y1)/2) + viewport), new Vector2((float)(x2-x1), (float)(y2-y1)));
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

	public void DoTexture(string texture, double x1, double y1, double x2, double y2, Color sprcolor)
	{
		if (((x1 > 512 && x2 > 512) || (x1 < 0 && x2 < 0)) && ((y1 > 512 && y2 > 512) || (y1 < 0 && y2 < 0))) return;	
		MySprite spr = MySprite.CreateSprite(texture, new Vector2((float)x1, (float)y1 + viewport), new Vector2((float)x2, (float)y2));
		spr.Color = sprcolor;
		drawframe.Add(spr);
	}

	public void DoText(string text, float size, string font, TextAlignment align, double x1, double y1, Color txtcolor)
	{
		if ((x1 > 512 || x1 < 0) && (y1 > 512 || y1 < -16*size)) return;
		MySprite txt = MySprite.CreateText(text, font, txtcolor, size, align);
		txt.Position = new Vector2((float)x1, (float)y1 + viewport);
		drawframe.Add(txt);
	}
	#endregion
}

class MyInfoBase
{
	public float Height = 50;
	public float Width = 512;
	public float Scale = 1f;
	public int Pages = 1;
	
	public string Title;
	
	public Color FGColor;
	public Color BGColor;
	public int Time;
	
	public MyInfoBase(string InitTitle, float InitWidth, float InitScale, Color InitFGColor, Color InitBGColor, int InitTime = 10)
	{
		Title = InitTitle;
		
		Width = InitWidth;
		Scale = InitScale;
		
		FGColor = InitFGColor;
		BGColor = InitBGColor;
		
		Time = InitTime;
	}
	
	public virtual float CalcHeight()
	{
		Height = 36*Scale;
		
		return Height;
	}
	
	public virtual void Draw(float PosX, float PosY, MySpriteDrawFrame DrawFrame, int Page = 0)
	{
		float OfsY = 0;
		var GraphFunc = new MyGraphFunc(DrawFrame);
				
		GraphFunc.DoText(Title, Scale*0.8f, "Debug", TextAlignment.LEFT, PosX + 12, PosY + 12 - (1 - Scale)*7, FGColor);
		OfsY += 32*Scale;
		
		GraphFunc.DoRectHollow(PosX + 6, PosY + 6, PosX + Width - 6, PosY + Height, 2, FGColor);
	}
}

class MyInvInfo : MyInfoBase
{
	public List<MyItem> InvList;
	public int SlotsPerLine;
	public int LinesPerPage;
	public int SlotsPerPage;
	public float SlotSize;
	
	public string SubTitle;
	
	public MyInvInfo(string InitTitle, string InitSubTitle, List<MyItem> InitInvList, float InitWidth, float InitMaxHeight, float InitScale, Color InitFGColor, Color InitBGColor, int InitTime = 10) : base(InitTitle, InitWidth, InitScale, InitFGColor, InitBGColor, InitTime)
	{
		SubTitle = InitSubTitle;
		
		InvList = InitInvList;
		
		SlotsPerLine = (int)Math.Floor((InitWidth-16)/Scale/48f);
		SlotSize = (InitWidth-16)/SlotsPerLine;
		
		LinesPerPage = (int)Math.Floor((InitMaxHeight - (SubTitle != "" ? 52*Scale : 36*Scale))/SlotSize);
		
		SlotsPerPage = LinesPerPage*SlotsPerLine;
		
		Pages = (InvList.Count - 1)/SlotsPerPage + 1;
	}
	
	public override float CalcHeight()
	{
		Height = 36*Scale;
		
		if (SubTitle != "")
			Height += 16*Scale;
		
		int PreLineNum = ((InvList.Count - 1)/SlotsPerLine + 1);
		if (PreLineNum > LinesPerPage)
			PreLineNum = LinesPerPage;
		
		Height += PreLineNum*SlotSize + 4;
		
		return Height;
	}
	
	public override void Draw(float PosX, float PosY, MySpriteDrawFrame DrawFrame, int Page = 0)
	{
		
		float OfsY = 0;
		var GraphFunc = new MyGraphFunc(DrawFrame);
				
		GraphFunc.DoText(Title, Scale*0.8f, "Debug", TextAlignment.LEFT, PosX + 12, PosY + 12 - (1 - Scale)*7, FGColor);
		
		if (Pages > 1)
			GraphFunc.DoText($"{Page + 1} / {Pages}", Scale*0.5f, "Debug", TextAlignment.RIGHT, PosX + Width - 12, PosY + 12 + (1 - Scale)*4.375, FGColor);
		
		OfsY += 36*Scale;
		
		if (SubTitle != "")
		{
			GraphFunc.DoText(SubTitle, Scale*0.5f, "Debug", TextAlignment.LEFT, PosX + 12, PosY + OfsY + (1 - Scale)*4.375, FGColor);
			OfsY += 16*Scale;
		}
		
		int SlotPX = 0;
		int SlotPY = 0;
		
		int ItemIndex = Page*SlotsPerPage;
		int ItemIndexMax = (Page + 1)*SlotsPerPage;
		if (ItemIndexMax > InvList.Count)
			ItemIndexMax = InvList.Count;
		
		while (ItemIndex < ItemIndexMax)
		{
			var Item = InvList[ItemIndex];
		
			float CurPX = PosX + 6 + SlotPX*SlotSize + 2;
			float CurPY = PosY + OfsY + SlotPY*SlotSize + 2;
			
			Color FGColorAlpha = new Color(FGColor.R, FGColor.G, FGColor.B, 32);
			
			GraphFunc.DoRectHollow(CurPX + 2, CurPY + 2, CurPX + SlotSize - 2, CurPY + SlotSize - 2, 2, FGColorAlpha);
			GraphFunc.DoTexture($"{Item.Type}", CurPX + SlotSize/2f, CurPY + SlotSize/2f, SlotSize - 10, SlotSize - 10, Color.White);
			
			double Amount = Item.Amount;
			
			string AmountStr = "";
			if (Amount > 1)
				AmountStr = $"{Math.Round(Amount)}";

			if (Item.TypeId == "MyObjectBuilder_Ore" || Item.TypeId == "MyObjectBuilder_Ingot")
			{
				var TypeStr = GetOreType(Item.SubtypeId);
				GraphFunc.DoText(TypeStr, Scale*0.5f, "Debug", TextAlignment.LEFT, CurPX + 6, CurPY + 2*Scale, FGColor);
				if (Amount >= 1000)
					AmountStr = $"{Math.Round(Amount/1000f, 2)}k";
			}
			GraphFunc.DoText(AmountStr, Scale*0.5f, "Debug", TextAlignment.LEFT, CurPX + 6, CurPY + SlotSize - 18*Scale, FGColor);
			
			ItemIndex ++ ;
			
			SlotPX ++ ; 
			if (SlotPX >= SlotsPerLine)
			{
				SlotPX = 0;
				SlotPY ++ ;
			}
		}
		OfsY += SlotSize*(SlotPY + 1) + 8;
		
		GraphFunc.DoRectHollow(PosX + 6, PosY + 6, PosX + Width - 6, PosY + Height, 2, FGColor);
	}
}

class MyInfoBar : MyInfoBase
{
	public List<BarInfo> PercentList;
	
	public MyInfoBar(string InitTitle, List<BarInfo> InitPercentList, float InitWidth, float InitScale, Color InitFGColor, Color InitBGColor, int InitTime = 10) : base(InitTitle, InitWidth, InitScale, InitFGColor, InitBGColor, InitTime)
	{
		PercentList = InitPercentList;
	}
	
	public override float CalcHeight()
	{
		Height = 40*Scale;
		foreach (BarInfo Bar in PercentList)
		{
			if (Bar.Title != "")
				Height += 22*Scale;	
			
			Height += 28*Scale;
		}
		Height += 8*Scale;
		
		return Height;
	}
	
	public override void Draw(float PosX, float PosY, MySpriteDrawFrame DrawFrame, int Page = 0)
	{
		
		float OfsY = 0;
		var GraphFunc = new MyGraphFunc(DrawFrame);
				
		GraphFunc.DoText(Title, Scale*0.8f, "Debug", TextAlignment.LEFT, PosX + 12, PosY + 12 - (1 - Scale)*7, FGColor);
		OfsY += 40*Scale;
		
		Color FGColorAlpha = new Color(FGColor.R, FGColor.G, FGColor.B, 16);
		
		foreach (BarInfo Bar in PercentList)
		{
			if (Bar.Title != "")
			{
				GraphFunc.DoText(Bar.Title, Scale*0.8f, "Debug", TextAlignment.LEFT, PosX + 12, PosY + OfsY - (1 - Scale)*7, FGColor);
				OfsY += 22*Scale;
			}
			
			float BarWidth = Width - 24;
			GraphFunc.DoRectHollow(PosX + 12, PosY + OfsY + 4, PosX + 12 + BarWidth, PosY + OfsY + 24*Scale, 2, FGColor);
			GraphFunc.DoRect(PosX + 12, PosY + OfsY + 4, PosX + 12 + BarWidth*(float)Bar.Percent, PosY + OfsY + 24*Scale, FGColor);
			OfsY += 28*Scale;
		}
		
		OfsY += 8*Scale;
		
		GraphFunc.DoRectHollow(PosX + 6, PosY + 6, PosX + Width - 6, PosY + Height, 2, FGColor);
	}
}

#region DS_var_and_func
int Step = 0;
int SurfaceIndex = 0;
int ArgIndex = 0;
int RunSpd = 1;

float Pos = 0;
float ViewY = 0;
float TotalHeight = 0;

class ScrollInfo
{
	public int Index = 0;
	public float Scroll = 0;
	public int Page = 0;
	
	public ScrollInfo()
	{
		Index = 0;
		Scroll = 0;
		Page = 0;
	}
}
List<ScrollInfo> ScrollInfoList;

int MaxTime = 0;

Color FGColor, BGColor;
Vector2 ScrSize;
float Viewport;
float Scale;

float MaxInvHeight;

MySpriteDrawFrame DrawFrame;

string ErrReason;

List<MyInfoBase> InfoList = new List<MyInfoBase>();

void AddInfo2List(MyInfoBase Info)
{
	TotalHeight += Info.CalcHeight();
	for (int i = 0; i < Info.Pages; i ++)
	{
		var ThisScroll = new ScrollInfo();
		ThisScroll.Index = InfoList.Count;
		ThisScroll.Scroll = TotalHeight - Info.Height/2f;
		ThisScroll.Page = i;
		ScrollInfoList.Add(ThisScroll);
	}
	InfoList.Add(Info);
}
#endregion

void DoMain()
{
	int Loops = 0;
	int MaxLoops = RunSpd;
	while ((Loops < MaxLoops) && (Surfaces.Count > 0))
	{
		var SurfaceInfo = Surfaces[SurfaceIndex];
		var Surface = SurfaceInfo.Surface;
			
		if (Surface != null)
		{
			#region DS_Init
			if (Step == 0)
			{
				Surface.ContentType = ContentType.SCRIPT;
				Surface.Script = "";
						
				FGColor = Surface.ScriptForegroundColor;
				BGColor = Surface.ScriptBackgroundColor;
				
				InfoList.Clear();
					
				ScrSize = Surface.SurfaceSize;
				Viewport = (Surface.TextureSize.Y - ScrSize.Y)/2f;
				
				MaxInvHeight = ScrSize.Y*.75f;
				
				Scale = 1f;
				
				RunSpd = 1;
				
				Step = 1;
				
				MaxTime = 10;
				
				Pos = TotalHeight = ViewY = 0;
				
				ScrollInfoList = new List<ScrollInfo>();
				
				DrawFrame = Surface.DrawFrame();
			}
			#endregion
			#region DS_Load
			if (Step == 1)
			{
				ErrReason = "";
				
				var SubArgs = SurfaceInfo.Args[ArgIndex].Split(',');
					
				int SubArgIndex;
					
				switch (SubArgs[0].ToLower())
				{
					#region item
					case "item":
						var ItemList = new List<MyItem>();
						var Title = "Inventory";
						var SubTitle = "";
						
						if (SubArgs.Length == 1)
						{
							Title = "Inventory";
							ItemList.AddRange(AllItemOnShip.AllItems);
						}
						else
						{
							var Items = new SortedItems();
							
							if (SubArgs[1] == "all")
								Items = AllItemOnShip;
							else if (GetInventory(false, Items, SubArgs[1]))
								SubTitle = $"in {SubArgs[1]}";
							else
								ErrReason = "item: Block / group doesn't exist.";
							
							if (ErrReason == "")
							{
								if (SubArgs.Length <= 2)
								{
									ItemList.AddRange(Items.AllItems);
								}
								else
								{
									Title = "";
									SubArgIndex = 2;
									while (SubArgIndex < SubArgs.Length)
									{
										if (SubArgIndex >= 3)
											Title += ", ";
										
										switch(SubArgs[SubArgIndex])
										{
											case "ore":
												ItemList.AddRange(Items.Ore);
												Title += "Ores";
												break;
											case "ingot":
												ItemList.AddRange(Items.Ingot);
												Title += "Ingots";
												break;
											case "component":
												ItemList.AddRange(Items.Component);
												Title += "Components";
												break;
											case "charitem":
												ItemList.AddRange(Items.CharItem);
												Title += "Character Items";
												break;
											case "ammo":
												ItemList.AddRange(Items.Ammo);
												Title += "Ammo";
												break;
											case "misc":
												ItemList.AddRange(Items.Misc);
												Title += "Misc";
												break;
											default:
												ErrReason = ERR_PARA;
												break;
										}
										
										SubArgIndex ++ ;
									}
								}
							}
						}
						
						if (ErrReason == "")
						{
							var ThisInfo = new MyInvInfo(Title, SubTitle, ItemList, ScrSize.X, MaxInvHeight, Scale, FGColor, BGColor, MaxTime);
							AddInfo2List(ThisInfo);
						}
						break;
					#endregion
					#region remain
					case "remain":
						var GetAll = false;
						var BarInfoList = new List<BarInfo>();
						
						double TotalVol, TotalMaxVol;
						var ThisBar = new BarInfo(.5, "");
						
						Title = "Space Usage";
						
						if (SubArgs.Length == 1)
							GetAll = true;
						else if (SubArgs[1] == "all")
							GetAll = true;
						else
						{
							var SpaceInfo = GetSpaceRemain(SubArgs[1]);
							if (SpaceInfo[0] != -1)
							{
								Title = $"{SubArgs[1]}";
								
								TotalVol = SpaceInfo[0];
								TotalMaxVol = SpaceInfo[1];
								ThisBar = new BarInfo(TotalVol/TotalMaxVol, $"{TotalVol}L / {TotalMaxVol}L");
							}
							else
								ErrReason = "remain: Block / group doesn't exist.";
						}
						
						if (GetAll)
						{
							TotalVol = Math.Round(AllItemOnShip.TotalVol);
							TotalMaxVol = Math.Round(AllItemOnShip.TotalMaxVol);
							ThisBar = new BarInfo(TotalVol/TotalMaxVol, $"{TotalVol}L / {TotalMaxVol}L");
						}
						
						BarInfoList.Add(ThisBar);
						if (ErrReason == "")
						{
							var ThisInfo = new MyInfoBar(Title, BarInfoList, ScrSize.X, Scale, FGColor, BGColor, MaxTime);
							AddInfo2List(ThisInfo);
						}
						break;
					#endregion
					#region remain2
					case "remain2":
						GetAll = false;
						BarInfoList = new List<BarInfo>();
						
						double TotalVolIn, TotalMaxVolIn, TotalVolOut, TotalMaxVolOut;
						var InBar = new BarInfo(.5, "");
						var OutBar = new BarInfo(.5, "");
						
						Title = "Production Space Usage";
						
						if (SubArgs.Length == 1)
							GetAll = true;
						else if (SubArgs[1] == "all")
							GetAll = true;
						else
							Title = SubArgs[1];
						
						var SpaceInfo2 = GetSpaceRemainProductor(SubArgs[1], GetAll);
						
						if (SpaceInfo2[0] != -1)
						{
							TotalVolIn = SpaceInfo2[0];
							TotalMaxVolIn = SpaceInfo2[1];
							TotalVolOut = SpaceInfo2[2];
							TotalMaxVolOut = SpaceInfo2[3];
							InBar = new BarInfo(TotalVolIn/TotalMaxVolIn, $"In: {TotalVolIn}L / {TotalMaxVolIn}L");
							OutBar = new BarInfo(TotalVolOut/TotalMaxVolOut, $"Out: {TotalVolOut}L / {TotalMaxVolOut}L");
							
							BarInfoList = new List<BarInfo>{ InBar, OutBar };
						}
						else
							ErrReason = "remain: Block / group doesn't exist.";

						if (ErrReason == "")
						{
							var ThisInfo = new MyInfoBar(Title, BarInfoList, ScrSize.X, Scale, FGColor, BGColor, MaxTime);
							AddInfo2List(ThisInfo);
						}
						break;
					#endregion
					#region setspd
					case "setspd":
						if (SubArgs.Length < 2)
							ErrReason = "setspd: " + ERR_ARG;
						else
						{
							int TargetSpd = 1;
							int.TryParse(SubArgs[1], out TargetSpd);
							if (TargetSpd < 1)
								ErrReason = "setspd: " + ERR_PARA;
							else
								RunSpd = TargetSpd;
						}
						MaxLoops ++ ;
						break;
					#endregion
					#region setheight
					case "setheight":
						if (SubArgs.Length < 2)
							ErrReason = "setheight: " + ERR_ARG;
						else
						{
							double ThisMaxHeight = 0;
							double.TryParse(SubArgs[1], out ThisMaxHeight);
							if (ThisMaxHeight < 128)
								ErrReason = "setheight: " + ERR_PARA;
							else
								MaxInvHeight = (float)ThisMaxHeight;
						}
						MaxLoops ++ ;
						break;
					#endregion
					#region settime
					case "settime":
						if (SubArgs.Length < 2)
							ErrReason = "settime: " + ERR_ARG;
						else
						{
							int TargetTime = 1;
							int.TryParse(SubArgs[1], out TargetTime);
							if (TargetTime < 1)
								ErrReason = "settime: " + ERR_PARA;
							else
								MaxTime = TargetTime;
						}
						MaxLoops ++ ;
						break;
					#endregion
					#region scale
					case "scale":
						if (SubArgs.Length < 2)
							ErrReason = "scale: " + ERR_ARG;
						else
						{
							double TargetScale = 1;
							double.TryParse(SubArgs[1], out TargetScale);
							if (TargetScale < 0)
								ErrReason = "scale: " + ERR_PARA;
							else
								Scale = (float)TargetScale;
						}
						MaxLoops ++ ;
						break;
					#endregion
					default:
						ErrReason = $"Unknown argument: {SubArgs[0].ToLower()}";
						MaxLoops ++ ;
						break;
				}
				
				if (ErrReason != "")
				{
					var ThisInfo = new MyInfoBase(ErrReason, ScrSize.X, Scale, FGColor, BGColor, MaxTime);
					AddInfo2List(ThisInfo);
				}
			}
			#endregion
			#region DS_Draw
			if (Step == 2)
			{
				var ThisScroll = ScrollInfoList[SurfaceInfo.Scroll];
				
				if ((Viewport - ViewY + Pos + InfoList[ArgIndex].Height < 0) || (Viewport - ViewY + Pos > ScrSize.Y + InfoList[ArgIndex].Height))
					MaxLoops ++ ;
				else
					InfoList[ArgIndex].Draw(0, Viewport - ViewY + Pos, DrawFrame, ThisScroll.Index == ArgIndex ? ThisScroll.Page : 0);
				
				Pos += InfoList[ArgIndex].Height;
			}
			#endregion
			
			#region DS_Next
			ArgIndex ++ ;
			
			if (ArgIndex == (Step == 1 ? SurfaceInfo.Args.Count : InfoList.Count))
			{
				ArgIndex = 0;
				
				if (TotalHeight > ScrSize.Y)
				{
					var GraphFunc = new MyGraphFunc(DrawFrame);
					GraphFunc.DoText($"{SurfaceInfo.Scroll + 1} / {ScrollInfoList.Count}", .7f, "Debug", TextAlignment.RIGHT, ScrSize.X-10, ScrSize.Y-26, FGColor);
					
					ViewY = ScrollInfoList[SurfaceInfo.Scroll].Scroll - ScrSize.Y/2f;
					if (ViewY < 0)
						ViewY = 0;
					else if (ViewY + ScrSize.Y > TotalHeight)
						ViewY = TotalHeight - ScrSize.Y;
				}
				else
					ViewY = 0;
				
				Step ++ ;
				
				if (InfoList.Count == 0)
					Step = 3;
				
				if (Step == 3)
				{
					GetInventory(true, AllItemOnShip);
					
					DrawFrame.Dispose();
					
					SurfaceInfo.Time ++ ;
					if (SurfaceInfo.Time > InfoList[ScrollInfoList[SurfaceInfo.Scroll].Index].Time)
					{
						SurfaceInfo.Scroll ++ ;
						SurfaceInfo.Time = 0;
					}
					if (SurfaceInfo.Scroll >= ScrollInfoList.Count)
						SurfaceInfo.Scroll = 0;
					
					SurfaceIndex ++ ;
					Step = 0;
				}
			}
			
			if (SurfaceIndex == Surfaces.Count)
				SurfaceIndex = 0;
			#endregion
		}
		Loops ++ ;
	}
}	

#region InvenroyyFunc
bool GetInventory(bool GetAll, SortedItems ItemCollection, string Name = "")
{
	ItemCollection.Cleanup();
	
	var AllEntities = new List<IMyEntity>();
	if (GetAll)
		GridTerminalSystem.GetBlocksOfType<IMyEntity>(AllEntities);
	else
	{
		var BlockGroup = GridTerminalSystem.GetBlockGroupWithName(Name);
		
		if (BlockGroup != null)
			BlockGroup.GetBlocksOfType<IMyEntity>(AllEntities);
		else
		{
			var Entity = GridTerminalSystem.GetBlockWithName(Name) as IMyEntity;
			if (Entity != null)
				AllEntities.Add(Entity);
			else return false;
		}
	}
	
	var ItemTemp = new List<MyInventoryItem>();
	var ExcludedTypes = new List<MyItemType>();
	foreach (IMyEntity Entity in AllEntities)
	{
		var Items = new List<MyInventoryItem>();
		
		IMyInventory Inventory;
		if (!(Entity is IMyProductionBlock))
			Inventory = Entity.GetInventory();
		else
			Inventory = (Entity as IMyProductionBlock).InputInventory;
		
		Inventory?.GetItems(Items);
		
		if (Inventory != null)
		{
			ItemCollection.TotalVol += Fix2Double(Inventory.CurrentVolume*1000);
			ItemCollection.TotalMaxVol += Fix2Double(Inventory.MaxVolume*1000);
		}
		
		ItemTemp.AddRange(Items);
		if (Entity is IMyProductionBlock)
		{
			Inventory = (Entity as IMyProductionBlock).OutputInventory;
		
			Inventory?.GetItems(Items);
			
			if (Inventory != null)
			{
				ItemCollection.TotalVol += Fix2Double(Inventory.CurrentVolume*1000);
				ItemCollection.TotalMaxVol += Fix2Double(Inventory.MaxVolume*1000);
			}
			
			ItemTemp.AddRange(Items);
		}
	}
	
	int i = 0;
	while (i < ItemTemp.Count)
	{
		var Item = ItemTemp[i++];
		var Next = false;
	
		int j = 0;
		while (j < ExcludedTypes.Count)
		{
			if (Item.Type == ExcludedTypes[j++])
			{
				Next = true;
				break;
			}
		}
		
		if (Next) continue;
		
		var NewItem = new MyItem(Item.Type.TypeId, Item.Type.SubtypeId, Fix2Double(Item.Amount));
		
		int k = 0;
		while (k < ItemTemp.Count)
		{
			var Item2 = ItemTemp[k++];
			if ((Item.Type == Item2.Type) && (Item != Item2))
				NewItem.Amount += Fix2Double(Item2.Amount);
		}
		
		ExcludedTypes.Add(Item.Type);
		ItemCollection.AllItems.Add(NewItem);
	}
	
	foreach (MyItem Item in ItemCollection.AllItems)
	{
		switch (Item.TypeId)
		{
			case "MyObjectBuilder_AmmoMagazine":
				if (Item.SubtypeId == "NATO_5p56x45mm")
					ItemCollection.CharItem.Add(Item);
				ItemCollection.Ammo.Add(Item);
				break;
			case "MyObjectBuilder_Component":
				ItemCollection.Component.Add(Item);
				break;
			case "MyObjectBuilder_Ore":
				ItemCollection.Ore.Add(Item);
				break;
			case "MyObjectBuilder_Ingot":
				ItemCollection.Ingot.Add(Item);
				break;
			case "MyObjectBuilder_PhysicalGunObject":
				ItemCollection.CharItem.Add(Item);
				break;
			case "MyObjectBuilder_ConsumableItem":
				ItemCollection.CharItem.Add(Item);
				break;
			case "MyObjectBuilder_Datapad":
				ItemCollection.CharItem.Add(Item);
				break;
			case "MyObjectBuilder_Package":
				ItemCollection.CharItem.Add(Item);
				break;
			case "MyObjectBuilder_PhysicalObject":
				ItemCollection.CharItem.Add(Item);
				break;
			case "MyObjectBuilder_OxygenContainerObject":
				ItemCollection.CharItem.Add(Item);
				break;
			case "MyObjectBuilder_GasContainerObject":
				ItemCollection.CharItem.Add(Item);
				break;
			default:
				ItemCollection.Misc.Add(Item);
				break;
		}
	}
	
	return true;
}

double[] GetSpaceRemain(string Name = "")
{
	var AllEntities = new List<IMyEntity>();
	
	var BlockGroup = GridTerminalSystem.GetBlockGroupWithName(Name);
		
	if (BlockGroup != null)
		BlockGroup.GetBlocksOfType<IMyEntity>(AllEntities);
	else
	{
		var Entity = GridTerminalSystem.GetBlockWithName(Name) as IMyEntity;
		if (Entity != null)
			AllEntities.Add(Entity);
		else return new double[2]{-1, -1};
	}
	
	double TotalVol = 0;
	double TotalMaxVol = 0;
	
	foreach (IMyEntity Entity in AllEntities)
	{
		var Inventory = Entity.GetInventory();
		if (Inventory != null)
		{
			TotalVol += Fix2Double(Inventory.CurrentVolume*1000);
			TotalMaxVol += Fix2Double(Inventory.MaxVolume*1000);
		}
	}
	
	return new double[2]{Math.Round(TotalVol), Math.Round(TotalMaxVol)};
}

double[] GetSpaceRemainProductor(string Name, bool GetAll = false)
{
	var ProductBlocks = new List<IMyProductionBlock>();
	
	if (GetAll)
		GridTerminalSystem.GetBlocksOfType<IMyProductionBlock>(ProductBlocks);
	else
	{
		var BlockGroup = GridTerminalSystem.GetBlockGroupWithName(Name);
			
		if (BlockGroup != null)
			BlockGroup.GetBlocksOfType<IMyProductionBlock>(ProductBlocks);
		else
		{
			var Block = GridTerminalSystem.GetBlockWithName(Name) as IMyProductionBlock;
			if (Block != null)
				ProductBlocks.Add(Block);
			else return new double[4]{-1, -1, -1, -1};
		}
	}
	
	double TotalVolIn = 0;
	double TotalMaxVolIn = 0;
	double TotalVolOut = 0;
	double TotalMaxVolOut = 0;
	
	foreach (IMyProductionBlock Block in ProductBlocks)
	{
		var Inventory = Block.InputInventory;
		if (Inventory != null)
		{
			TotalVolIn += Fix2Double(Inventory.CurrentVolume*1000);
			TotalMaxVolIn += Fix2Double(Inventory.MaxVolume*1000);
		}
		
		Inventory = Block.OutputInventory;
		if (Inventory != null)
		{
			TotalVolOut += Fix2Double(Inventory.CurrentVolume*1000);
			TotalMaxVolOut += Fix2Double(Inventory.MaxVolume*1000);
		}
	}
	
	return new double[4]{Math.Round(TotalVolIn), Math.Round(TotalMaxVolIn), Math.Round(TotalVolOut), Math.Round(TotalMaxVolOut)};
}
#endregion

void GetSurfaces(bool GetNew = false)
{
	Surfaces.Clear();

	var AllBlocks = new List<IMyTerminalBlock>();
	
	GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(AllBlocks);
	
	foreach (IMyTerminalBlock Block in AllBlocks)
	{
		if ((Block is IMyTextSurface) || (Block is IMyTextSurfaceProvider))
		{
			if (Block.CustomData.StartsWith("InventoryLCD"))
			{
				var Args = Block.CustomData.Split((char)10);
			
				int ArgIndex = 1;
				int ListIndex = 0;
				
				MySurface SurfaceInfo = new MySurface();
				
				List<MySurface> SurfaceList = new List<MySurface>();
				
				if (Block is IMyTextSurface)
					SurfaceInfo.Surface = Block as IMyTextSurface;
				else
					SurfaceInfo.Surface = (Block as IMyTextSurfaceProvider).GetSurface(0);
				
				SurfaceList.Add(SurfaceInfo);
				
				while (ArgIndex < Args.Length)
				{
					var CurArg = Args[ArgIndex];
					
					if (CurArg.ToLower().StartsWith("lcd") && (Block is IMyTextSurfaceProvider))
					{
						var SubArgs = CurArg.Split(',');
					
						int LCDNum = 0;
						int.TryParse(SubArgs[1], out LCDNum);
						
						var Surface = (Block as IMyTextSurfaceProvider).GetSurface(LCDNum);
						
						if (ListIndex == 0)
							SurfaceList[0].Surface = Surface;
						else 
						{
							SurfaceInfo = new MySurface();
							
							SurfaceInfo.Surface = Surface;
							
							SurfaceList.Add(SurfaceInfo);
							
							ListIndex ++ ;
						}
						
						ArgIndex ++ ;
						continue;
					}
					
					if (!CurArg.StartsWith("#"))
						SurfaceList[ListIndex].Args.Add(CurArg);
					
					ArgIndex ++ ;
				}
				
				Surfaces.AddRange(SurfaceList);
			}
		}
	}
	
	ArgIndex = 0;
}

void Initalize()
{
	Runtime.UpdateFrequency = UpdateFrequency.Update1;
	GetInventory(true, AllItemOnShip);
	GetSurfaces();
}

public Program()
{
	Initalize();
}

public void Main(string Argument, UpdateType UpdateSource)
{
	if (Argument == "Refresh")
		GetSurfaces();
	
	DoMain();
	Echo($"Inventory LCD by 次硅酸钙\n\nInstruction count: {Runtime.CurrentInstructionCount.ToString()} / {Runtime.MaxInstructionCount.ToString()}\nExcute time : {Math.Round(ExcuteTime = Math.Max(ExcuteTime - (ExcuteTime - Runtime.LastRunTimeMs)/10, Runtime.LastRunTimeMs), 4)}ms");
}

//tetrisgame.cs by 次硅酸钙
//version 1.5B 2019-08-20

//驾驶室/座椅操作：
//	A/D				左右移动
//	S				加速落下

//	Q				逆时针旋转
//	W/E				顺时针旋转

//	空格/离开座位	暂停
//	C				文字/彩色模式切换

//运行参数：
//	left
//	right			左右移动

//	drop			加速落下

//	cw
//	ccw				顺/逆时针旋转

//	pause			暂停
//	reset			重置游戏
//	textmode		文字/彩色模式切换

//文字模式	 	 	方块只有一种颜色 高帧率
//彩色模式	 	 	彩色方块 低帧率

string LCD_Name_1 = "tetris_lcd_1";		//液晶屏名称
string Player1_Controller = "cockpit";	//驾驶室/座椅
bool textMode = false;					//是否使用文字模式




//Don't touch the script below
//Block data

/*
	0-1		Z1
	2-3		Z2
	4-7		L1
	8-11	L2
	12-15	T
	16		口
	17-18	|

byte[,] baz1 = new byte[4,4]{	//Z1
	{0,0,0,0}, 
	{0,0,1,0}, 
	{0,1,1,0}, 
	{0,1,0,0}};
byte[,] baz2 = new byte[4,4]{
	{0,0,0,0}, 
	{0,0,0,0}, 
	{0,1,1,0}, 
	{0,0,1,1}};
byte[,] bbz1 = new byte[4,4]{	//Z2
	{0,0,0,0}, 
	{0,1,0,0}, 
	{0,1,1,0}, 
	{0,0,1,0}};
byte[,] bbz2 = new byte[4,4]{
	{0,0,0,0}, 
	{0,0,0,0}, 
	{0,0,1,1}, 
	{0,1,1,0}};
byte[,] bal1 = new byte[4,4]{	//L1
	{0,0,0,0}, 
	{0,1,0,0}, 
	{0,1,1,1}, 
	{0,0,0,0}};
byte[,] bal2 = new byte[4,4]{
	{0,0,0,0}, 
	{0,0,1,1}, 
	{0,0,1,0}, 
	{0,0,1,0}};
byte[,] bal3 = new byte[4,4]{
	{0,0,0,0}, 
	{0,0,0,0}, 
	{0,1,1,1}, 
	{0,0,0,1}};
byte[,] bal4 = new byte[4,4]{
	{0,0,0,0}, 
	{0,0,1,0}, 
	{0,0,1,0}, 
	{0,1,1,0}};
byte[,] bbl1 = new byte[4,4]{	//L2
	{0,0,0,0}, 
	{0,0,0,0}, 
	{0,1,1,1}, 
	{0,1,0,0}};
byte[,] bbl2 = new byte[4,4]{
	{0,0,0,0}, 
	{0,1,1,0}, 
	{0,0,1,0}, 
	{0,0,1,0}};
byte[,] bbl3 = new byte[4,4]{
	{0,0,0,0}, 
	{0,0,0,1}, 
	{0,1,1,1}, 
	{0,0,0,0}};
byte[,] bbl4 = new byte[4,4]{
	{0,0,0,0}, 
	{0,0,1,0}, 
	{0,0,1,0}, 
	{0,0,1,1}};
byte[,] bat1 = new byte[4,4]{	//T
	{0,0,0,0}, 
	{0,0,1,0}, 
	{0,1,1,1}, 
	{0,0,0,0}};
byte[,] bat2 = new byte[4,4]{
	{0,0,0,0}, 
	{0,0,1,0}, 
	{0,0,1,1}, 
	{0,0,1,0}};
byte[,] bat3 = new byte[4,4]{
	{0,0,0,0}, 
	{0,0,0,0}, 
	{0,1,1,1}, 
	{0,0,1,0}};
byte[,] bat4 = new byte[4,4]{
	{0,0,0,0}, 
	{0,0,1,0}, 
	{0,1,1,0}, 
	{0,0,1,0}};
byte[,] bao = new byte[4,4]{	//Square
	{0,0,0,0}, 
	{0,1,1,0}, 
	{0,1,1,0}, 
	{0,0,0,0}};
byte[,] bai1 = new byte[4,4]{	//Line
	{0,0,0,0}, 
	{0,0,0,0}, 
	{1,1,1,1}, 
	{0,0,0,0}};
byte[,] bai2 = new byte[4,4]{
	{0,1,0,0}, 
	{0,1,0,0}, 
	{0,1,0,0}, 
	{0,1,0,0}};
*/
	
//string LCD_Name_1 = "tetris_lcd_1";		//液晶屏名称
//string LCD_Name_2 = "tetris_lcd_2";
//string Player1_Seat = "seat1";			//玩家1座椅名

string version = "1.5B";
string date = "2019-08-20";

string controlsText = $"Controls:\nA/D - Movement\nS - Drop\nQ/W/E - Rotation\nSpacebar - Pause\nC - Text/color mode\nPause when leave";
	
byte[,,] piecedata = new byte[20,4,4]{{{0,0,0,0},{0,0,1,0},{0,1,1,0},{0,1,0,0}},{{0,0,0,0},{0,0,0,0},{0,1,1,0},{0,0,1,1}},{{0,0,0,0},{0,1,0,0},{0,1,1,0},{0,0,1,0}},{{0,0,0,0},{0,0,0,0},{0,0,1,1},{0,1,1,0}},{{0,0,0,0},{0,1,0,0},{0,1,1,1},{0,0,0,0}},{{0,0,0,0},{0,0,1,1},{0,0,1,0},{0,0,1,0}},{{0,0,0,0},{0,0,0,0},{0,1,1,1},{0,0,0,1}},{{0,0,0,0},{0,0,1,0},{0,0,1,0},{0,1,1,0}},{{0,0,0,0},{0,0,0,0},{0,1,1,1},{0,1,0,0}},{{0,0,0,0},{0,1,1,0},{0,0,1,0},{0,0,1,0}},{{0,0,0,0},{0,0,0,1},{0,1,1,1},{0,0,0,0}},{{0,0,0,0},{0,0,1,0},{0,0,1,0},{0,0,1,1}},{{0,0,0,0},{0,0,1,0},{0,1,1,1},{0,0,0,0}},{{0,0,0,0},{0,0,1,0},{0,0,1,1},{0,0,1,0}},{{0,0,0,0},{0,0,0,0},{0,1,1,1},{0,0,1,0}},{{0,0,0,0},{0,0,1,0},{0,1,1,0},{0,0,1,0}},{{0,0,0,0},{0,1,1,0},{0,1,1,0},{0,0,0,0}},{{0,0,0,0},{0,0,0,0},{1,1,1,1},{0,0,0,0}},{{0,1,0,0},{0,1,0,0},{0,1,0,0},{0,1,0,0}},{{0,0,0,0},{0,0,0,0},{0,0,0,0},{0,0,0,0}}};

byte[] idtable = new byte[7]{1,3,6,8,14,16,17};

Color[] colorPalette = new Color[7]{new Color(160,0,0),new Color(0,160,0),new Color(160,160,0),new Color(0,0,160),new Color(160,0,160),new Color(0,160,160),new Color(160,160,160)};
string[] textPalette = new string[7]{"1","(",")","[","]","{","}"};

//byte[,] stored = new byte[4,4];
byte[] tbclear = new byte[5];

Random rand = new Random();

byte[,] blocktable = new byte[30,10];
short input = 0;
short p_x = 2;
short p_y = 8;
short p_xold, p_yold;
short piece = 0;
short pieceold = 0;
short next = 0;
short color = 0;
short nextcolor = 0;
short oldcolor;
byte level = 0;
long score = 0;
int lines = 0;
int curlines = 0;

byte menu = 0;
byte timer = 0;
short intimer = 0;
bool gameover = false;
bool hit = false;
bool drop = false;
bool started = false;
bool paused = false;
bool pressed = false;
bool pressed2 = false;
bool Cpressed = false;
bool dropped = false;
bool controllerMode;
bool IsUnderControlold;

Program(){
    Runtime.UpdateFrequency = UpdateFrequency.Update1;
	gameInitialize();
}

void gameInitialize() {
	input = 0;
	p_x = 2;
	p_y = 8;
	piece = 19;
	pieceold = 19;
	next = 19;
	color = 0;
	nextcolor = 0;
	level = 0;
	score = 0;
	lines = 0;
	curlines = 0;

	menu = 0;
	timer = 0;
	intimer = 0;
	gameover = false;
	hit = false;
	drop = false;
	started = false;
	paused = false;
	pressed = false;
	pressed2 = true;
	Cpressed = false;
	dropped = false;
	clearBoard();
}

void clearBoard() {
	byte y = 0;
	while (y<30) {
		byte x = 0;
		while (x<10) {
			blocktable[y,x++] = 0;
		}
		y ++ ;
	}
}

void drawScreen(string panel_name) {
	var panel_ = GridTerminalSystem.GetBlockWithName(panel_name) as IMyTextPanel;
	if (panel_==null) {
		Echo($">LCD panel {panel_name}?");
		return;
	}
	if (!textMode) {
		panel_.ContentType = ContentType.SCRIPT;
		panel_.Script = "";
		Color maincolor_ = new Color(255,255,255);
		panel_.ScriptForegroundColor = maincolor_;
		panel_.ScriptBackgroundColor = new Color(0,0,0);
		using (var buffer_ = panel_.DrawFrame()) {
			MySprite title = MySprite.CreateText("SETetris", "Debug", maincolor_, 1f,TextAlignment.CENTER);
				title.Position= new Vector2(256,0);
				buffer_.Add(title);
			MySprite nexttxt = MySprite.CreateText("Next:", "Debug", maincolor_, 0.8f,TextAlignment.LEFT);
				nexttxt.Position= new Vector2(282,70);
				buffer_.Add(nexttxt);
			MySprite scoretxt = MySprite.CreateText($"Score:\n{score}", "Debug", maincolor_, 0.8f,TextAlignment.LEFT);
				scoretxt.Position= new Vector2(282,185);
				buffer_.Add(scoretxt);
			MySprite linetxt = MySprite.CreateText($"Lines:\n{lines}", "Debug", maincolor_, 0.8f,TextAlignment.LEFT);
				linetxt.Position= new Vector2(282,230);
				buffer_.Add(linetxt);
			if (timer<90) {
				MySprite lvltxt = MySprite.CreateText($"Level:\n{level}", "Debug", maincolor_, 0.8f,TextAlignment.LEFT);
					lvltxt.Position= new Vector2(282,275);
					buffer_.Add(lvltxt);
			}
			MySprite helptxt = MySprite.CreateText(controlsText, "Debug", maincolor_, 0.45f,TextAlignment.CENTER);
				helptxt.Position= new Vector2(330,335);
				buffer_.Add(helptxt);
			if (gameover) {
				MySprite gameovertxt = MySprite.CreateText("GAME OVER", "Debug", maincolor_, 1f,TextAlignment.CENTER);
					gameovertxt.Position= new Vector2(256,32);
					buffer_.Add(gameovertxt);
			} else if (menu==0) {
				MySprite starttip = MySprite.CreateText("Press any key to start", "Debug", maincolor_, 1f,TextAlignment.CENTER);
					starttip.Position= new Vector2(256,32);
					buffer_.Add(starttip);
			} else if (menu==1) {
				MySprite leveltip = MySprite.CreateText("Press A/D to select level, S to start", "Debug", maincolor_, 1f,TextAlignment.CENTER);
					leveltip.Position= new Vector2(256,32);
					buffer_.Add(leveltip);
			} else if (paused) {
				MySprite gameovertxt = MySprite.CreateText("PAUSED", "Debug", maincolor_, 1f,TextAlignment.CENTER);
					gameovertxt.Position= new Vector2(256,32);
					buffer_.Add(gameovertxt);
			}
			//main
			byte y = 0;
			while (y<20) {
				byte x = 0;
				while (x<10) {
					if (blocktable[y+10,x]!=0) {
						int color = blocktable[y+10,x]-1;
						if(blocktable[y+10,x]>=0x81) color=blocktable[y+10,x]-0x81;
						MySprite square = MySprite.CreateSprite("SquareSimple", new Vector2(96f+16f*x,96f+16f*y), new Vector2(12f,12f));
							square.Color = colorPalette[color];
							buffer_.Add(square);
					}
					x++;
				}
				y++;
			}
			//next
			y = 0;
			while (y<4) {
				byte x = 0;
				while (x<4) {
					if (piecedata[next,y,x++]!=0) {
						MySprite square = MySprite.CreateSprite("SquareSimple", new Vector2(304f+16f*x,118f+16f*y), new Vector2(12f,12f));
							square.Color = colorPalette[nextcolor];
							buffer_.Add(square);
					}
				}
				y++;
			}
			MySprite border = MySprite.CreateSprite("SquareHollow", new Vector2(168f,248f), new Vector2(180f,350f));
				border.Color = maincolor_;
				buffer_.Add(border);
			MySprite bordernext = MySprite.CreateSprite("SquareHollow", new Vector2(344f,142f), new Vector2(80f,80f));
				bordernext.Color = maincolor_;
				buffer_.Add(bordernext);
		}
	} else {
		panel_.ContentType = ContentType.TEXT_AND_IMAGE;
		panel_.Alignment = TextAlignment.LEFT;
		Color maincolor_ = panel_.ScriptForegroundColor;
		panel_.FontSize = .75f;
		string output = $"SETetris\n";
		if (gameover) output+=$"GAME OVER\n         ";
		else if (menu==0) output+=$"Press any key to start\n\n{controlsText}";
		else if (menu==1) output+=$"Press A/D to select level, S to start\n         ";
		if (timer<90&menu==1) output+=$"Level {level}";
		else if (paused) output += $"PAUSED\n         ";
		else output += $"\n         ";
		if (started) {
			byte y = 0;
			while (y<20) {
				byte x = 0;
				while (x<10) {
					if (blocktable[y+10,x]!=0) {
						byte z = 0;
						int blockTxt = blocktable[y+10,x]-1;
						if(blocktable[y+10,x]>=0x81) blockTxt=blocktable[y+10,x]-0x81;
						while (z<3) {
							output += $"{textPalette[blockTxt]}";
							z++;
						}
						output += " ";
					} else {
						output += "... ";
					}
					x++;
				}
				if (y==1) output+="         Next:";
				else if (y>=2&&y<=5) {
					output += "         ";
					x = 0;
					while (x<4) {
						if (piecedata[next,y-2,x++]!=0) {
							byte z = 0;
							while (z<3) {
								output += $"{textPalette[nextcolor]}";
								z++;
							}
							output += " ";
						} else {
							output += "... ";
						}
					}
				} else if (y==7) output+="         Score:";
				else if (y==8) output+=$"         {score}";
				else if (y==10) output+=$"         Lines:";
				else if (y==11) output+=$"         {lines}";
				else if (y==13) output+=$"         Level:";
				else if (y==14) output+=$"         {level}";
				output += $"\n         ";
				y++;
			}
		}
		panel_.WriteText(output);
	}
		
}
void setBlockz(int px, int py, int id, short val) {
	byte y = 0;
	while (y<4) {
		byte x = 0;
		while (x<4) {
			if ((px+x>=0&px+x<10)&(py+y>=0&py+y<30)) {
				if (piecedata[id,y,x]!=0) blocktable[py+y,px+x]=(byte)val;
			}
			x++;
		}
		y++;
	}
}
/*
void storeBlockz(int px, int py, int id, byte val) {
	byte y = 0;
	while (y<4) {
		byte x = 0;
		while (x<4) {
			if ((px+x>=0&px+x<10)&(py+y>=0&py+y<30)) {
				if (val==0) {
					if (piecedata[id,y,x]!=0&blocktable[py+y,px+x]!=0) {
						stored[y,x]=blocktable[y,x];
						blocktable[py+y,px+x]=0;
					}
				} else if (stored[y,x]!=0) blocktable[py+y,px+x]=stored[y,x];
			}
			x++;
		}
		y++;
	}
}
*/
void updateBlockz(int newx, int newy, int oldx, int oldy, short newid, short oldid, short color_) {
	setBlockz(oldx, oldy, oldid, 0);
	setBlockz(newx, newy, newid, (short)(0x81+color_));
}

bool collideTest(int px, int py, short id) {
	bool ret = false;
//	storeBlockz(oldx, oldy, oldid, 0);
	byte y = 0;
	while (y<4) {
		byte x = 0;
		while (x<4) {
			if (piecedata[id,y,x]!=0) {
				Echo($"F{x},{y},P{px+x},{py+y}");
				if ((px+x<0|px+x>=10)|py+y>=30) {
					Echo(">>CollideA");
					ret = true;
				}
				if ((px+x>=0&px+x<10)&(py+y>=0&py+y<30)) {
					if (blocktable[py+y,px+x]>0&blocktable[py+y,px+x]<0x81) {
						Echo(">>CollideB");
						ret = true;
					}
				}
			}
			x++;
		}
		y++;
	}
	Echo(">>end");
//	storeBlockz(oldx, oldy, oldid, 1);
	return ret;
}

short rotate(short oldid, short dir) {
/*	if (dir==-1) {
		Echo("<<Rotate");
		if (oldid==0|oldid==2|oldid==17) {
			return (short)(oldid+1);
		} else if (oldid==4|oldid==8|oldid==12) {
			return (short)(oldid+3);
		} else {
			if (oldid!=16) return (short)(oldid-1);
			else return oldid;
		}
	} else if (dir==1) {
		Echo(">>Rotate");
		if (oldid==1|oldid==3|oldid==18) {
			return (short)(oldid-1);
		} else if (oldid==7|oldid==11|oldid==15) {
			return (short)(oldid-3);
		} else {
			if (oldid!=16) return (short)(oldid+1);
			else return oldid;
		}
	}
	return oldid;*/
	switch (dir) {
		case -1:
			Echo("<<Rotate");
			if (oldid==0|oldid==2|oldid==17) {
				Echo("A");
				return (short)(oldid+1);
			} else if (oldid==4|oldid==8|oldid==12) {
				Echo("B");
				return (short)(oldid+3);
			} else {
				if (oldid!=16) {
					Echo("C1");
					return (short)(oldid-1);
				} else {
					Echo("C2");
					return oldid;
				}
			}
		case 1:
			Echo(">>Rotate");
			if (oldid==1|oldid==3|oldid==18) {
				Echo("A");
				return (short)(oldid-1);
			} else if (oldid==7|oldid==11|oldid==15) {
				Echo("B");
				return (short)(oldid-3);
			} else {
				if (oldid!=16) {
					Echo("C1");
					return (short)(oldid+1);
				} else {
					Echo("C2");
					return oldid;
				}
			}
		default:
			return oldid;
	}		
}

void removePieces() {
	byte y = 29;
	byte empty = 0;
	while (y>1) {
		byte x = 0;
		byte count = 0;
		while (x<10) {
			if (blocktable[y,x]>0&blocktable[y,x]<0x81) count++;
			x++;
		}
		if (count==10) {
			tbclear[1+empty++] = y;
			byte z=0;
			while (z<10) {
				blocktable[y,z]=0;
				z++;
			}
			Echo($"<A:{y}");
		}
		y--;
	}
	lines = lines + empty;
	curlines = curlines + empty;
	if (empty!=0) score=Convert.ToInt32(Math.Round(score+10*Math.Pow(2,empty)*Math.Pow(level+1, 2)));
	else score=score+3+level;
	while (empty>0) {
		y = tbclear[empty--];
		Echo($">B:{y}");
		while (y>1) {
			byte x = 0;
			x = 0;
			while (x<10) {
				blocktable[y,x]=blocktable[y-1,x];
				x++;
			}
			y--;
		}
	}
}

void gameUpdate() {
	if (!gameover) {
		if (started&!paused) {
			if (input==1) {
				if (!collideTest(p_x+1,p_y,piece)&intimer<=0) {
					p_x++;
					intimer = 6;
				} else intimer--;
			} else if (input==-1) {
				if (!collideTest(p_x-1,p_y,piece)&intimer<=0) {
					p_x--;
					intimer = 6;
				} else intimer--;
			} else if (input==2) {
				piece = rotate(piece, 1);
				if (collideTest(p_x,p_y,piece)) piece=pieceold;
			} else if (input==-2) {
				piece = rotate(piece, -1);
				if (collideTest(p_x,p_y,piece)) piece=pieceold;
			} else if (input==3) {
				if (!dropped) drop=true;
			} else {
				intimer = 0;
				drop = false;
				dropped = false;
				pressed2 = false;
			}
			
			if (curlines>Convert.ToInt32(10*Math.Pow(2,level))) {
				curlines = curlines-Convert.ToInt32(10*Math.Pow(2,level));
				level++;
			}
			if (level>12) level=12;
			
			timer++;
			if (timer>=30-2*level|(drop&timer>=1)) {
				if (collideTest(p_x,p_y+1,piece)) {
					if (p_x==2&p_y==8) gameover=true;
					p_x = 2;
					p_y = 8;
					color = nextcolor;
					piece = next;
					next = idtable[(short)rand.Next(7)];
					nextcolor = (short)rand.Next(7);
					dropped = hit = true;
				} else p_y ++ ;
				timer = 0;
			}
			
			if (hit) {
				setBlockz(p_xold,p_yold,pieceold,(short)(oldcolor+1));
				drop = hit = false;
				removePieces();
			} else {
				updateBlockz(p_x,p_y,p_xold,p_yold,piece,pieceold,color);
			}
			
			Echo($"{piece}\n{p_x},{p_y},{p_xold},{p_yold}");
			
			oldcolor = color;
			pieceold = piece;
			p_xold = p_x;
			p_yold = p_y;
		}
	}
	
	if (menu==1) {
		timer++;
		if (timer>120) timer=60;
		if (input==-1&!pressed2) {
			if (level>0) level--;
			pressed2 = true;
			timer = 60;
		} else if (input==1&!pressed2) {
			if (level<9) level++;
			pressed2 = true;
			timer = 60;
		} else if (input==3&!pressed2) {
			menu=2;
		}
		if (input==0) pressed2=false;
	}
	
	if (input!=0) {
		if (!pressed2) {
			if (menu==0) {
				menu = 1;
				timer = 60;
			}
			if (gameover&!pressed) {
				gameInitialize();
			/*	paused = false;
				started = true;
				gameover = false;
				color = (short)rand.Next(7);
				nextcolor = (short)rand.Next(7);
				pieceold = piece = idtable[(short)rand.Next(7)];
				next = idtable[(short)rand.Next(7)];*/
			}
			pressed2 = true;
		}
		input = 0;
	} else {
		if (menu==2) {
			menu++;
			started = true;
			gameover = false;
			color = (short)rand.Next(7);
			nextcolor = (short)rand.Next(7);
			pieceold = piece = idtable[(short)rand.Next(7)];
			next = idtable[(short)rand.Next(7)];
		}
		pressed2=false;
	}
}

void Main(string argument, UpdateType updateSource){
	switch (argument.ToLower()) {
		case "reset":
			gameInitialize();
			break;
		case "pause":
			if (started&!gameover) paused=!paused;
			break;
		case "left":
			input = -1;
			break;
		case "right":
			input = 1;
			break;
		case "ccw":
			input = -2;
			break;
		case "cw":
			input = 2;
			break;
		case "drop":
			input = 3;
			break;
		case "textmode":
			textMode=!textMode;
			break;
		default:
			break;
	}
    if ((updateSource & UpdateType.Update1) != 0) {
		Echo($"SETetris by 次硅酸钙\n{version} {date}");
		var controller = GridTerminalSystem.GetBlockWithName(Player1_Controller) as IMyShipController;
		controllerMode = (controller!=null);
		if (controller!=null) {
			//X		A -1; D 1;
			//Y		C -1; Spacebar 1;
			//Z		W -1; S 1;
			Echo($"Controller : {Player1_Controller}\n");
			Echo($"UnderControl : {controller.IsUnderControl}\n");
			var plrInputDir = controller.MoveIndicator;
			int inputId = (int)plrInputDir.Z+(int)plrInputDir.X*10+(int)plrInputDir.Y*1000+(int)controller.RollIndicator*100;
			if (IsUnderControlold!=controller.IsUnderControl|!controller.IsUnderControl) paused=!controller.IsUnderControl;
			switch (inputId) {
				case -1:
					if (!pressed) input=2;
					break;
				case 1:
					input = 3;
					break;
				case -10:
					input = -1;
					break;
				case 10:
					input = 1;
					break;
				case -100:
					if (!pressed) input=-2;
					break;
				case 100:
					if (!pressed) input=2;
					break;
				case 1000:
					if (started&!gameover&!pressed) paused=!paused;
					break;
				case -1000:
					if (!Cpressed) textMode=!textMode;
					Cpressed = true;
					break;
				default:
					pressed = Cpressed = false;
					break;
			}
			if (!gameover) {
				if (inputId!=0&started) pressed=true;
			} else if (inputId==0) pressed=false;
			Echo($"{inputId}, prs-{pressed}");
			IsUnderControlold = controller.IsUnderControl;
		}
		gameUpdate();
		drawScreen(LCD_Name_1);
		Echo($"\n{Runtime.CurrentInstructionCount} / {Runtime.MaxInstructionCount}");
	}
}
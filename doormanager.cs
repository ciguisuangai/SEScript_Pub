//doormanager.cs by ciguisuangai
//github.com/ciguisuangai
//20-01-31
//20-03-01

//自动关闭未关闭的舱门
//每个气闸室的门的分组带上[Airlock]标签（区分大小写），每一组仅获取前两个门

const int CloseDoorTicks = 30;	//设定关门时长

//--------------------

List<IMyDoor> Doors = new List<IMyDoor>();
List<int> DoorTime = new List<int>();
List<MyAirlock> Airlocks = new List<MyAirlock>();

int DoorFound = 0;
int AirlockFound = 0;

//--------------------

double ExcuteTime = 0;

//--------------------

public class MyAirlock {
	public List<IMyDoor> AirlockDoors = new List<IMyDoor>();
	public List<int> AirlockDoorTime = new List<int>();
	public MyAirlock() {
		AirlockDoors.Clear();
		AirlockDoorTime.Clear();
	}
	public void UpdateTimer() {
		AirlockDoorTime.Clear();
		int i = 0;
		while (i < AirlockDoors.Count) {
			AirlockDoorTime.Add(0);
			i ++ ;
		}
	}
	public void Update() {
		int i = 0;
		foreach (IMyDoor Door in AirlockDoors) {
			if (Door.Status != DoorStatus.Closed) AirlockDoorTime[i] ++ ;
				else AirlockDoorTime[i] = 0;
			if (AirlockDoorTime[i] > CloseDoorTicks) Door?.CloseDoor();
			i ++ ;
		}
		AirlockDoors[0].Enabled = (AirlockDoors[1].OpenRatio == 0);
		AirlockDoors[1].Enabled = (AirlockDoors[0].OpenRatio == 0);
	}
}

//--------------------

public void GetDoors() {
	
	DoorFound = 0;
	AirlockFound = 0;
	
	Doors.Clear();
	DoorTime.Clear();
	Airlocks.Clear();
	
	var AllDoors = new List<IMyDoor>();
	var AllBlockGroups = new List<IMyBlockGroup>();
	var ExcludedDoors = new List<IMyDoor>();
	GridTerminalSystem.GetBlockGroups(AllBlockGroups);
	
	foreach (IMyBlockGroup BlockGroup in AllBlockGroups) {
		
		var DoorsInGroup = new List<IMyDoor>();
		
		if (BlockGroup.Name.Contains("[Airlock]")) {
			BlockGroup.GetBlocksOfType<IMyDoor>(DoorsInGroup);
			var TempDoors = new List<IMyDoor>();
			
			int i = 0;
			foreach (IMyDoor Door in DoorsInGroup) {
				if (!(Door is IMyAirtightHangarDoor)) {
					TempDoors.Add(Door);
					i ++ ;
				}
				if (i == 2) {
					var TempAirlock = new MyAirlock();
					TempAirlock.AirlockDoors = TempDoors;
					Airlocks.Add(TempAirlock);
					i ++ ;
					AirlockFound ++ ;
				}
				ExcludedDoors.Add(Door);
			}
		}
	}
	
	foreach (MyAirlock Airlock in Airlocks) Airlock.UpdateTimer();
	
	GridTerminalSystem.GetBlocksOfType<IMyDoor>(AllDoors);
	
	foreach (IMyDoor Door in AllDoors) {
		if (ExcludedDoors.Count != 0) {
			bool Added = false;
			foreach (IMyDoor ExcludedDoor in ExcludedDoors)
				if (Door != ExcludedDoor && !(Door is IMyAirtightHangarDoor) && !Added) {
					Doors.Add(Door);
					DoorTime.Add(0);
					Added = true;
					DoorFound ++ ;
				}
		} else if (!(Door is IMyAirtightHangarDoor)) {
			Doors.Add(Door);
			DoorTime.Add(0);
			DoorFound ++ ;
		}
	}
}

public void ProcessDoors() {
	foreach (MyAirlock Airlock in Airlocks) Airlock.Update();
	
	var i = 0;
	foreach (IMyDoor Door in Doors) {
		if (Door.Status != DoorStatus.Closed) DoorTime[i] ++ ;
			else DoorTime[i] = 0;
		if (DoorTime[i] > CloseDoorTicks) Door?.CloseDoor();
		i ++ ;
	}
}

public Program() {
	Runtime.UpdateFrequency = UpdateFrequency.Update10;
	GetDoors();
}

public void Main(string Argument, UpdateType UpdateSource) {
	if (UpdateSource != UpdateType.Update10) return;
	
	ProcessDoors();
	
	Echo($"-- Door Manager --\nby ciguisuangai\n\nRecompile to refresh.\n\nDoor(s) found: {DoorFound}\nAirlock(s) found: {AirlockFound}\n\nExcute time : {Math.Round(ExcuteTime = Math.Max(ExcuteTime - (ExcuteTime - Runtime.LastRunTimeMs)/2, Runtime.LastRunTimeMs), 4)}ms");
}

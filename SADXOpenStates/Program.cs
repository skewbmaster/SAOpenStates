using System;
using System.Threading;
using SharpDX.XInput;
using Memory;
using Newtonsoft.Json;

// Used for later
using System.Configuration;
using UserSettings = SADXOpenStates.Properties.Settings;


namespace SADXOpenStates
{
    class Program
    {
        public static XController CONTROLLER; // Initialise controller for inputs

        public static Mem m = new Mem(); // Initialise memory dll

        public static Thread checkGame = new Thread(checkForProcess);
        public static bool gameHooked = false;

        //public static int[] memAddresses = { 0x03742E10, 0x0370EF35, 0x0370F128, 0x0370EF48, 0x0370EF34, 0x0370F0E4 }; // Character 1 offset, Time:(Frames, Seconds, Minutes), Lives, Rings

        public static SaveState[] saveStates = new SaveState[10]; // Create 10 save states
        public static int curSaveState = 0;

        static void Main(string[] args)
        {
            if(System.IO.File.Exists("file.txt"))
            {
                string json = System.IO.File.ReadAllText("file.txt");
                SaveStateSerializer SavesObject = JsonConvert.DeserializeObject<SaveStateSerializer>(json);

                Console.WriteLine(SavesObject.Version);

                SaveState[] statesFromFile = SavesObject.SaveStates;
                for (int i = 0; i < 10; i++)
                {
                    if(statesFromFile[i] != null)
                    {
                        statesFromFile[i] = new SaveState(statesFromFile[i].xPos, statesFromFile[i].yPos, statesFromFile[i].zPos, statesFromFile[i].xRot, statesFromFile[i].yRot, statesFromFile[i].zRot, statesFromFile[i].hSpeed, statesFromFile[i].vSpeed, statesFromFile[i].hover, statesFromFile[i].lives, statesFromFile[i].rings, statesFromFile[i].tFrames, statesFromFile[i].tSeconds, statesFromFile[i].tMins, statesFromFile[i].camX, statesFromFile[i].camY, statesFromFile[i].camZ, statesFromFile[i].camXRot, statesFromFile[i].camYRot, statesFromFile[i].camZRot);
                    }
                }
            }

            ConnectController();
            Hook();
            Run();
        }

        public static bool Hook()
        {
            Console.WriteLine("Searching for game...");

            //Console.WriteLine(m.GetProcIdFromName("sonic"));

            while (true)
            {
                if (!m.OpenProcess("sonic.exe"))
                {
                    System.Threading.Thread.Sleep(1000);
                    continue;
                }

                gameHooked = true;
                Console.WriteLine("Game found!");
                checkGame.Start();
                return true;
            }
        }

        public static void checkForProcess()
        {
            while (true)
            {
                if (m.GetProcIdFromName("sonic") == 0)
                {
                    m.CloseProcess();
                    gameHooked = false;
                    Console.WriteLine("Game closed");
                    break;
                }

                Thread.Sleep(1000);
            }
        }

        public static void ConnectController()
        {
            Console.WriteLine("Searching for controller...");

            while (true)
            {
                CONTROLLER = new XController(UserIndex.One); // Define controller as the first one plugged in

                if (CONTROLLER.GetConnected()) break;

                System.Threading.Thread.Sleep(100);
            }

            Console.WriteLine("Controller found!");
        }

        public static void Run()
        {
            GamepadButtonFlags buttonsPressed;

            bool hasSaved = false, hasLoaded = false, hasSwitched = false;
            bool left, right, up, down;

            while (true)
            {
                while (gameHooked)
                {

                    if (CONTROLLER == null)
                    {
                        ConnectController();
                    }
                    else if (!CONTROLLER.GetConnected())
                    {
                        CONTROLLER = null;
                        Console.WriteLine("Controller disconnected");
                        continue;
                    }


                    buttonsPressed = CONTROLLER.GetState().Gamepad.Buttons;

                    left = buttonsPressed.HasFlag(GamepadButtonFlags.DPadLeft);
                    right = buttonsPressed.HasFlag(GamepadButtonFlags.DPadRight);
                    up = buttonsPressed.HasFlag(GamepadButtonFlags.DPadUp);
                    down = buttonsPressed.HasFlag(GamepadButtonFlags.DPadDown);

                    if (left && !hasSaved)
                    {
                        saveStates[curSaveState] = new SaveState(m);
                        

                        Console.WriteLine("Saved to {0}", curSaveState);
                        SaveStateSerializer SaveObjectToSerialize = new SaveStateSerializer(saveStates);
                        string json = JsonConvert.SerializeObject(SaveObjectToSerialize);
                        
                        System.IO.File.WriteAllText("file.txt", json);

                        hasSaved = true;
                    }
                    else if (!left && hasSaved)
                    {
                        hasSaved = false;
                    }


                    if (right && !hasLoaded && saveStates[curSaveState] != null)
                    {
                        LoadState(saveStates[curSaveState]);

                        Console.WriteLine("Loaded {0}", curSaveState);

                        hasLoaded = true;
                    }
                    else if (right && saveStates[curSaveState] == null && !hasLoaded)
                    {
                        hasLoaded = true;
                        Console.WriteLine("Cannot find save state {0}", curSaveState);
                    }
                    else if (!right && hasLoaded)
                    {
                        hasLoaded = false;
                    }
                    


                    if ((up || down) && !hasSwitched)
                    {
                        //Console.WriteLine(Convert.ToInt32(UserSettings.Default.invertCycle));
                        curSaveState += Convert.ToInt32(up);
                        curSaveState -= Convert.ToInt32(down);

                        if (curSaveState == -1) curSaveState = 9;
                        else if (curSaveState == 10) curSaveState = 0;

                        Console.WriteLine("Switched to save slot {0}", curSaveState);

                        hasSwitched = true;
                    }
                    else if (!(up || down) && hasSwitched)
                    {
                        hasSwitched = false;
                    }


                    System.Threading.Thread.Sleep(10);
                }

                checkGame = new Thread(checkForProcess);
                Hook();
            }
        }

        public static void LoadState(SaveState state)
        {
            // Write Position into memory
            m.WriteMemory("base+03742E10,20", "float", state.xPos.ToString());
            m.WriteMemory("base+03742E10,24", "float", state.yPos.ToString());
            m.WriteMemory("base+03742E10,28", "float", state.zPos.ToString());

            //m.WriteMemory("base+0372CAB0", "float", state.xPos.ToString());
            //m.WriteMemory("base+0372CAB4", "float", state.xPos.ToString());
            //m.WriteMemory("base+0372CAB8", "float", state.xPos.ToString());

            // Write Rotation into memory
            m.WriteMemory("base+03742E10,14", "2bytes", state.xRot.ToString());
            m.WriteMemory("base+03742E10,18", "2bytes", state.yRot.ToString());
            m.WriteMemory("base+03742E10,1C", "2bytes", state.zRot.ToString());

            // Write Speed into memory
            m.WriteMemory("base+373CDF0,38", "float", state.hSpeed.ToString());
            m.WriteMemory("base+373CDF0,3C", "float", state.vSpeed.ToString());
            m.WriteMemory("base+373CDF0,8", "2bytes", state.hover.ToString());

            // Write Lives and rings into memory
            m.WriteMemory("base+0370EF34", "int", state.lives.ToString());
            m.WriteMemory("base+0370F0E4", "int", state.rings.ToString());

            // Write time into memory
            m.WriteMemory("base+0370EF35", "int", state.tFrames.ToString());
            m.WriteMemory("base+0370F128", "int", state.tSeconds.ToString());
            m.WriteMemory("base+0370EF48", "int", state.tMins.ToString());



            // Write camera info into memory
            if (m.ReadByte("base+372CBA8") != 7)
            {
                m.WriteMemory("base+372CBA8", "byte", "4");

                m.WriteMemory("base+0372CBB0,20", "float", state.camX.ToString());
                m.WriteMemory("base+0372CBB0,24", "float", state.camY.ToString());
                m.WriteMemory("base+0372CBB0,28", "float", state.camZ.ToString());

                m.WriteMemory("base+0372CBB0,14", "2bytes", state.camXRot.ToString());
                m.WriteMemory("base+0372CBB0,18", "2bytes", state.camYRot.ToString());
                m.WriteMemory("base+0372CBB0,1C", "2bytes", state.camZRot.ToString());

                //m.WriteMemory("base+372CAE1", "byte", "0");
            }
        }
    }

    class XController
    {
        private Controller controller;

        public XController(UserIndex index) 
        {
            this.controller = new Controller(index);
        }

        public State GetState() 
        {
            return controller.GetState();
        }

        public bool GetConnected()
        {
            return controller.IsConnected;
        }
    }

    public class SaveStateSerializer
    {
        public string Version { get; set; }
        public SaveState[] SaveStates { get; set; }
        [JsonConstructor]
        public SaveStateSerializer(SaveState[] saves)
        {
            Version = "1";
            SaveStates = saves;
        }
    }

    public class SaveState
    {
        public float xPos, yPos, zPos, xRot, yRot, zRot, hSpeed, vSpeed;
        public int lives, rings, hover, tFrames, tSeconds, tMins;

        public float camX, camY, camZ;
        public int camXRot, camYRot, camZRot;

        public SaveState(Mem memwatch)
        {

            this.xPos = memwatch.ReadFloat("base+03742E10,20");
            this.yPos = memwatch.ReadFloat("base+03742E10,24");
            this.zPos = memwatch.ReadFloat("base+03742E10,28");
            this.xRot = memwatch.Read2Byte("base+03742E10,14");
            this.yRot = memwatch.Read2Byte("base+03742E10,18");
            this.zRot = memwatch.Read2Byte("base+03742E10,1C");

            this.hSpeed = memwatch.ReadFloat("base+373CDF0,38");
            this.vSpeed = memwatch.ReadFloat("base+373CDF0,3C");
            this.hover = memwatch.Read2Byte("base+373CDF0,8");

            this.lives = memwatch.ReadByte("base+0370EF34");
            this.rings = memwatch.Read2Byte("base+0370F0E4");

            this.tFrames = memwatch.ReadByte("base+0370EF35");
            this.tSeconds = memwatch.ReadByte("base+0370F128");
            this.tMins = memwatch.ReadByte("base+0370EF48");

            this.camX = memwatch.ReadFloat("base+0372CBB0,20");
            this.camY = memwatch.ReadFloat("base+0372CBB0,24");
            this.camZ = memwatch.ReadFloat("base+0372CBB0,28");
            this.camXRot = memwatch.Read2Byte("base+0372CBB0,14");
            this.camYRot = memwatch.Read2Byte("base+0372CBB0,18");
            this.camZRot = memwatch.Read2Byte("base+0372CBB0,1C");
        }
        [JsonConstructor]
        public SaveState(float xPos, float yPos, float zPos, float xRot, float yRot, float zRot, float hSpeed, float vSpeed, int hover, int lives, int rings, int tFrames, int tSeconds, int tMins, float camX, float camY, float camZ, int camXRot, int camYRot, int camZRot)
        {
            this.xPos = xPos;
            this.yPos = yPos;
            this.zPos = zPos;
            this.xRot = xRot;
            this.yRot = yRot;
            this.zRot = zRot;

            this.hSpeed = hSpeed;
            this.vSpeed = vSpeed;
            this.hover = hover;

            this.lives = lives;
            this.rings = rings;

            this.tFrames = tFrames;
            this.tSeconds = tSeconds;
            this.tMins = tMins;

            this.camX = camX;
            this.camY = camY;
            this.camZ = camZ;
            this.camXRot = camXRot;
            this.camYRot = camYRot;
            this.camZRot = camZRot;
        }
    }
}

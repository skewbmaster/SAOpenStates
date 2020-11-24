using System;
using System.Threading;
using SharpDX.XInput;
using SharpDX.DirectInput;
using Memory;
using Newtonsoft.Json;
using System.Linq;
using System.IO;

// Used for later
using System.Configuration;
using UserSettings = SADXOpenStates.Properties.Settings;


namespace SADXOpenStates
{
    class Program
    {
        public static XController CONTROLLER; // Initialise controller for inputs
        public static DController DCONTROLLER;

        public static Mem m = new Mem(); // Initialise memory dll

        public static Thread checkGame = new Thread(checkForProcess);
        public static bool gameHooked = false;

        //public static int[] memAddresses = { 0x03742E10, 0x0370EF35, 0x0370F128, 0x0370EF48, 0x0370EF34, 0x0370F0E4 }; // Character 1 offset, Time:(Frames, Seconds, Minutes), Lives, Rings

        public static SaveState[] saveStates = new SaveState[10]; // Create 10 save states
        public static int curSaveState = 0;

        public static bool isDInput = false;

        static void Main(string[] args)
        {
            if (File.Exists("file.txt"))
            {
                string json = File.ReadAllText("file.txt");
                SaveStateSerializer SavesObject = JsonConvert.DeserializeObject<SaveStateSerializer>(json);

                saveStates = SavesObject.SaveStates;
            }

            if (File.Exists("DInput.txt"))
            {
                string json = File.ReadAllText("DInput.txt");
                DCONTROLLER = JsonConvert.DeserializeObject<DController>(json);
                isDInput = true;

                DCONTROLLER.InitializeController();
            }
            else
            {
                ConnectController();
            }

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
            Console.WriteLine("Searching for XInput controller...");

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
            int buttonsPressed;

            bool hasSaved = false, hasLoaded = false, hasSwitched = false;
            bool DLeft, DRight, DUp, DDown, LB;

            int invertCycle = UserSettings.Default.invertCycle ? -1 : 1;

            while (true)
            {
                while (gameHooked)
                {
                    if (!isDInput)
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

                        buttonsPressed = (int)CONTROLLER.GetState().Gamepad.Buttons;

                        DUp = (buttonsPressed & 1) != 0 ? true : false;
                        DDown = (buttonsPressed & 2) != 0 ? true : false;
                        DLeft = (buttonsPressed & 4) != 0 ? true : false;
                        DRight = (buttonsPressed & 8) != 0 ? true : false;
                        LB = (buttonsPressed & 256) != 0 ? true : false;
                    } 
                    else
                    {
                        bool[] Dinputs = DCONTROLLER.GetState();
                        if(!Dinputs[5])
                        {
                            DCONTROLLER.InitializeController();
                            continue;
                        }

                        DUp = Dinputs[0];
                        DLeft = Dinputs[1];
                        DRight = Dinputs[2];
                        DDown = Dinputs[3];
                        LB = Dinputs[4];
                    }

                    if(LB || !UserSettings.Default.extraInput)
                    {
                        if (DLeft && !hasSaved)
                        {
                            saveStates[curSaveState] = new SaveState(m);


                            Console.WriteLine("Saved to {0}", curSaveState);
                            SaveStateSerializer SaveObjectToSerialize = new SaveStateSerializer(saveStates);
                            string json = JsonConvert.SerializeObject(SaveObjectToSerialize);

                            System.IO.File.WriteAllText("file.txt", json);

                            hasSaved = true;
                        }
                        else if (!DLeft && hasSaved)
                        {
                            hasSaved = false;
                        }


                        if (DRight && !hasLoaded)
                        {
                            if (saveStates[curSaveState] != null)
                            {
                                LoadState(saveStates[curSaveState]);

                                Console.WriteLine("Loaded {0}", curSaveState);
                            }
                            else
                            {
                                Console.WriteLine("Cannot find save state {0}", curSaveState);
                            }
                            hasLoaded = true;
                        }
                        else if (!DRight && hasLoaded)
                        {
                            hasLoaded = false;
                        }



                        if ((DUp || DDown) && !hasSwitched)
                        {
                            //Console.WriteLine(Convert.ToInt32(UserSettings.Default.invertCycle));

                            curSaveState += Convert.ToInt32(DUp) * invertCycle;
                            curSaveState -= Convert.ToInt32(DDown) * invertCycle;

                            if (curSaveState == -1) curSaveState = 9;
                            else if (curSaveState == 10) curSaveState = 0;

                            Console.WriteLine("Switched to save slot {0}", curSaveState);

                            hasSwitched = true;
                        }
                        else if (!(DUp || DDown) && hasSwitched)
                        {
                            hasSwitched = false;
                        }

                        System.Threading.Thread.Sleep(10);
                    }
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

    class DController
    {
        public int?[,] Buttons;
        public int?[,] DPad;

        private static DirectInput directInput = new DirectInput();
        private static Joystick joystick;

        public bool controllerIsConnected = false;

        [JsonConstructor]
        public DController(int?[,] dpadArray, int?[,] buttonsArray)
        {
            DPad = dpadArray;
            Buttons = buttonsArray;
        }

        public void InitializeController()
        {
            Console.WriteLine("Searching for DInput controller...");
            // Find a Joystick Guid
            var joystickGuid = Guid.Empty;

            // If Joystick not found, throws an error
            while (joystickGuid == Guid.Empty)
            {
                foreach (var deviceInstance in directInput.GetDevices(SharpDX.DirectInput.DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices))
                    joystickGuid = deviceInstance.InstanceGuid;

                if (joystickGuid == Guid.Empty)
                    foreach (var deviceInstance in directInput.GetDevices(SharpDX.DirectInput.DeviceType.Joystick, DeviceEnumerationFlags.AllDevices))
                        joystickGuid = deviceInstance.InstanceGuid;

                Thread.Sleep(100);
            }

            // Instantiate the joystick
            joystick = new Joystick(directInput, joystickGuid);

            // Found Gamepad

            // Set BufferSize in order to use buffered data.
            //joystick.Properties.BufferSize = 0;

            // Acquire the joystick
            joystick.Acquire();

            controllerIsConnected = true;
            Console.WriteLine("Controller found!");
        }

        public bool[] GetState()
        {
            bool[] states = new bool[6];
            JoystickState state;
            try
            {
                joystick.Poll();
                state = joystick.GetCurrentState();
            }
            catch
            {
                return new bool[] { false, false, false, false, false, false };
            }


            for (int i = 0; i < 4; i++)
            {
                if(DPad[i, 0] == null)
                {
                    states[i] = state.Buttons[(int)DPad[i, 1]];
                }
                else
                {
                    states[i] = state.PointOfViewControllers[(int)DPad[i, 0]] == DPad[i,1] ? true : false;
                }
            }
            if (Buttons[0, 0] == null)
            {
                states[4] = state.Buttons[(int)Buttons[0, 1]];
            }
            else
            {
                states[4] = state.PointOfViewControllers[(int)Buttons[0, 0]] == Buttons[0, 1] ? true : false;
            }
            states[5] = true;
            return states;
            //state.Buttons[1];
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

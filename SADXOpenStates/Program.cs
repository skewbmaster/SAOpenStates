using System;
using System.Threading;
using System.Linq;
using System.IO;
using System.Diagnostics;

using SharpDX.XInput;
using SharpDX.DirectInput;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using System.Configuration;
using UserSettings = SADXOpenStates.Properties.Settings;


namespace SADXOpenStates
{
    class Program
    {
        public static Process gameProc = new Process();
        public static XController CONTROLLER; // Initialise controller for inputs
        public static DController DCONTROLLER;
        public static int baseAddress;

        public static bool gameHooked = false;
        
        public static SaveState[] saveStates = new SaveState[10]; // Create 10 save states
        public static int curSaveState = 0;

        public static bool isDInput = false;

        static void Main(string[] args)
        {
            if (File.Exists("states.json"))
            {
                using (StreamReader reader = File.OpenText("states.json"))
                using (JsonTextReader treader = new JsonTextReader(reader))
                {
                    JObject jsonObj = (JObject)JToken.ReadFrom(treader);
                    //jsonObj.Value<string>("Version"); //SaveState file version, useful for later when there's gonna be updates to the savestates themselves
                    
                    saveStates = jsonObj["SaveStates"].ToObject<SaveState[]>();
                }
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

            //Console.WriteLine(ProcessMemory.GetProcIdFromName("sonic"));

            while (true)
            {
                try { gameProc = Process.GetProcessesByName("sonic").First(); }
                catch
                {
                    try { gameProc = Process.GetProcessesByName("Sonic Adventure DX").First(); }
                    catch
                    {
                        Thread.Sleep(1000);
                        continue;
                    }
                }

                gameProc.Exited += GameProc_Exited;
                gameProc.EnableRaisingEvents = true;
                gameHooked = true;
                baseAddress = gameProc.MainModule.BaseAddress.ToInt32();
                Console.WriteLine("Game found!");
                return true;
            }
        }

        private static void GameProc_Exited(object sender, EventArgs e)
        {
            gameProc.Exited -= GameProc_Exited;
            gameHooked = false;
            Console.WriteLine("Game closed");
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

            byte gameState = 21;

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
                        if (!Dinputs[5])
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

                    if (LB || !UserSettings.Default.extraInput)
                    {
                        if (DLeft && !hasSaved)
                        {
                            gameState = ProcessMemory.ReadByte(gameProc, baseAddress + 0x3722DE4);
                            if (gameState != 16 && gameState != 21)
                            {
                                saveStates[curSaveState] = new SaveState(ref gameProc, ref baseAddress);

                                Console.WriteLine("Saved to {0}", curSaveState);
                                SaveStateSerializer SaveObjectToSerialize = new SaveStateSerializer(saveStates);
                                string json = JsonConvert.SerializeObject(SaveObjectToSerialize, Formatting.Indented);

                                System.IO.File.WriteAllText("states.json", json);
                            }
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
                                if (ProcessMemory.ReadByte(gameProc, baseAddress + 0x3722DE4) != 16)
                                {
                                    LoadState(saveStates[curSaveState]);

                                    Console.WriteLine("Loaded {0}", curSaveState);
                                }
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
                            if (ProcessMemory.ReadByte(gameProc, baseAddress + 0x3722DE4) != 16)
                            {
                                //Console.WriteLine(Convert.ToInt32(UserSettings.Default.invertCycle));

                                curSaveState += Convert.ToInt32(DUp) * invertCycle;
                                curSaveState -= Convert.ToInt32(DDown) * invertCycle;

                                if (curSaveState == -1) curSaveState = 9;
                                else if (curSaveState == 10) curSaveState = 0;

                                Console.WriteLine("Switched to save slot {0}", curSaveState);

                            }
                            hasSwitched = true;
                        }
                        else if (!(DUp || DDown) && hasSwitched)
                        {
                            hasSwitched = false;
                        }

                        System.Threading.Thread.Sleep(10);
                    }
                }

                Hook();
            }
        }

        public static void LoadState(SaveState state)
        {
            // Write Position into memory
            ProcessMemory.Write(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x03742E10, 0x20), state.xPos);
            ProcessMemory.Write(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x03742E10, 0x24), state.yPos);
            ProcessMemory.Write(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x03742E10, 0x28), state.zPos);

            // Write Rotation into memory
            ProcessMemory.Write(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x03742E10, 0x14), state.xRot);
            ProcessMemory.Write(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x03742E10, 0x18), state.yRot);
            ProcessMemory.Write(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x03742E10, 0x1C), state.zRot);

            // Write Speed into memory
            ProcessMemory.Write(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x0373CDF0, 0x38), state.hSpeed);
            ProcessMemory.Write(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x0373CDF0, 0x3C), state.vSpeed);
            ProcessMemory.Write(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x0373CDF0, 0x8), state.hover);

            // Write Lives and rings into memory
            ProcessMemory.Write(gameProc, baseAddress + 0x0370EF34, state.lives);
            ProcessMemory.Write(gameProc, baseAddress + 0x0370F0E4, state.rings);

            // Write time into memory
            ProcessMemory.Write(gameProc, baseAddress + 0x0370EF35, state.tFrames);
            ProcessMemory.Write(gameProc, baseAddress + 0x0370F128, state.tSeconds);
            ProcessMemory.Write(gameProc, baseAddress + 0x0370EF48, state.tMins);

            // Write more player info
            ProcessMemory.Write(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x03742E10, 0x0), state.action);
            ProcessMemory.Write(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x0373CDF0, 0x124), state.anim);
            ProcessMemory.Write(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x0373CDF0, 0x130), state.animFrame);
            ProcessMemory.Write(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x03742E10, 0x4), state.playerStates);
            ProcessMemory.Write(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x03742E10, 0x5), state.playerStates2);


            // Write camera info into memory
            if (ProcessMemory.ReadByte(gameProc, 0x372CBA8) != 7)
            {
                ProcessMemory.Write(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x0372CBB0, 0x20), state.camX);
                ProcessMemory.Write(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x0372CBB0, 0x24), state.camY);
                ProcessMemory.Write(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x0372CBB0, 0x28), state.camZ);

                ProcessMemory.Write(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x0372CBB0, 0x14), state.camXRot);
                ProcessMemory.Write(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x0372CBB0, 0x18), state.camYRot);
                ProcessMemory.Write(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x0372CBB0, 0x1C), state.camZRot);
                
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
                if (DPad[i, 0] == null)
                {
                    states[i] = state.Buttons[(int)DPad[i, 1]];
                }
                else
                {
                    states[i] = state.PointOfViewControllers[(int)DPad[i, 0]] == DPad[i, 1] ? true : false;
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
            Version = "2";
            SaveStates = saves;
        }
    }

    public class SaveState
    {
        public float xPos, yPos, zPos, hSpeed, vSpeed, animFrame;

        public byte tFrames, tSeconds, tMins, lives, action, anim, playerStates, playerStates2;
        public short xRot, yRot, zRot, rings, hover;


        public float camX, camY, camZ;
        public short camXRot, camYRot, camZRot;

        public const SaveState Empty = default(SaveState);
        public SaveState(ref Process gameProc, ref int baseAddress)
        {
            this.xPos = ProcessMemory.ReadSingle(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x03742E10, 0x20));
            this.yPos = ProcessMemory.ReadSingle(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x03742E10, 0x24));
            this.zPos = ProcessMemory.ReadSingle(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x03742E10, 0x28));
            this.xRot = ProcessMemory.ReadInt16(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x03742E10, 0x14));
            this.yRot = ProcessMemory.ReadInt16(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x03742E10, 0x18));
            this.zRot = ProcessMemory.ReadInt16(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x03742E10, 0x1C));

            this.hSpeed = ProcessMemory.ReadSingle(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x0373CDF0, 0x38));
            this.vSpeed = ProcessMemory.ReadSingle(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x0373CDF0, 0x3C));
            this.hover = ProcessMemory.ReadInt16(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x0373CDF0, 0x8));

            this.lives = ProcessMemory.ReadByte(gameProc, baseAddress + 0x0370EF34);
            this.rings = ProcessMemory.ReadInt16(gameProc, baseAddress + 0x0370F0E4);

            this.tFrames = ProcessMemory.ReadByte(gameProc, baseAddress + 0x0370EF35);
            this.tSeconds = ProcessMemory.ReadByte(gameProc, baseAddress + 0x0370F128);
            this.tMins = ProcessMemory.ReadByte(gameProc, baseAddress + 0x0370EF48);

            this.camX = ProcessMemory.ReadSingle(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x0372CBB0, 0x20));
            this.camY = ProcessMemory.ReadSingle(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x0372CBB0, 0x24));
            this.camZ = ProcessMemory.ReadSingle(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x0372CBB0, 0x28));
            this.camXRot = ProcessMemory.ReadInt16(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x0372CBB0, 0x14));
            this.camYRot = ProcessMemory.ReadInt16(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x0372CBB0, 0x18));
            this.camZRot = ProcessMemory.ReadInt16(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x0372CBB0, 0x1C));

            this.action = ProcessMemory.ReadByte(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x03742E10, 0x0));
            this.anim = ProcessMemory.ReadByte(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x0373CDF0, 0x124));
            this.animFrame = ProcessMemory.ReadByte(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x0373CDF0, 0x130));
            this.playerStates = ProcessMemory.ReadByte(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x03742E10, 0x4));
            this.playerStates2 = ProcessMemory.ReadByte(gameProc, ProcessMemory.GetFinalAddress(gameProc, baseAddress + 0x03742E10, 0x5));
        }
        [JsonConstructor]
        public SaveState(float xPos, float yPos, float zPos, short xRot, short yRot, short zRot, float hSpeed, float vSpeed, short hover, byte lives, short rings, byte tFrames, byte tSeconds, byte tMins, float camX, float camY, float camZ, short camXRot, short camYRot, short camZRot, byte action, byte anim, float animFrame, byte playerStates, byte playerStates2)
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

            this.action = action;
            this.anim = anim;
            this.animFrame = animFrame;
            this.playerStates = playerStates;
            this.playerStates2 = playerStates2;
        }
    }
}

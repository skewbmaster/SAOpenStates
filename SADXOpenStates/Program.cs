using System;
using SharpDX.XInput;
using Memory;

// Used for later
using System.Configuration;
using UserSettings = SADXOpenStates.Properties.Settings;


namespace SADXOpenStates
{
    class Program
    {
        public static XController CONTROLLER; // Initialise controller for inputs

        public static Mem m = new Mem(); // Initialise memory dll

        //public static int[] memAddresses = { 0x03742E10, 0x0370EF35, 0x0370F128, 0x0370EF48, 0x0370EF34, 0x0370F0E4 }; // Character 1 offset, Time:(Frames, Seconds, Minutes), Lives, Rings

        public static SaveState[] saveStates = new SaveState[10]; // Create 10 save states
        public static int curSaveState = 0;

        static void Main(string[] args)
        {
            Init();
            Run();
        }

        public static void Init()
        {
            CONTROLLER = new XController(UserIndex.One); // Define controller as the first one plugged in

            Hook();
        }

        public static bool Hook()
        {
            Console.WriteLine("Searching for game...");

            while (true)
            {
                if (!m.OpenProcess("sonic.exe"))
                {
                    System.Threading.Thread.Sleep(1000);
                    continue;
                }

                Console.WriteLine("Game found!");
                return true;
            }
        }

        public static void Run()
        {
            GamepadButtonFlags buttonsPressed;

            bool hasSaved = false, hasLoaded = false, hasSwitched = false;

            while (true) {

                buttonsPressed = CONTROLLER.GetState().Gamepad.Buttons;


                if (buttonsPressed.HasFlag(GamepadButtonFlags.DPadLeft) && !hasSaved)
                {
                    saveStates[curSaveState] = new SaveState(m);

                    Console.WriteLine("Saved to {0}", curSaveState);

                    hasSaved = true;
                }
                else if (!buttonsPressed.HasFlag(GamepadButtonFlags.DPadLeft) && hasSaved)
                {
                    hasSaved = false;
                }


                if (buttonsPressed.HasFlag(GamepadButtonFlags.DPadRight) && !hasLoaded && saveStates[curSaveState] != null)
                {
                    LoadState(saveStates[curSaveState]);

                    Console.WriteLine("Loaded {0}", curSaveState);

                    hasLoaded = true;
                }
                else if (!buttonsPressed.HasFlag(GamepadButtonFlags.DPadRight) && hasLoaded)
                { 
                    hasLoaded = false; 
                }


                if ((buttonsPressed.HasFlag(GamepadButtonFlags.DPadUp) || buttonsPressed.HasFlag(GamepadButtonFlags.DPadDown)) && !hasSwitched)
                {
                    curSaveState += Convert.ToInt32(buttonsPressed.HasFlag(GamepadButtonFlags.DPadUp));
                    curSaveState -= Convert.ToInt32(buttonsPressed.HasFlag(GamepadButtonFlags.DPadDown));

                    if (curSaveState == -1) curSaveState = 9;
                    else if (curSaveState == 10) curSaveState = 0;

                    Console.WriteLine("Switched to save slot {0}", curSaveState);

                    hasSwitched = true;
                }
                else if (!(buttonsPressed.HasFlag(GamepadButtonFlags.DPadUp) || buttonsPressed.HasFlag(GamepadButtonFlags.DPadDown)) && hasSwitched)
                {
                    hasSwitched = false;
                }
            }
        }

        public static void LoadState(SaveState state)
        {
            // Write Position into memory
            m.WriteMemory("base+03742E10,20", "float", state.xPos.ToString());
            m.WriteMemory("base+03742E10,24", "float", state.yPos.ToString());
            m.WriteMemory("base+03742E10,28", "float", state.zPos.ToString());

            m.WriteMemory("base+0372CAB0", "float", state.xPos.ToString());
            m.WriteMemory("base+0372CAB4", "float", state.xPos.ToString());
            m.WriteMemory("base+0372CAB8", "float", state.xPos.ToString());

            // Write Rotation into memory
            m.WriteMemory("base+03742E10,14", "2bytes", state.xRot.ToString());
            m.WriteMemory("base+03742E10,18", "2bytes", state.yRot.ToString());
            m.WriteMemory("base+03742E10,1C", "2bytes", state.zRot.ToString());

            // Write Speed into memory
            m.WriteMemory("base+373CDF0,38", "float", state.hSpeed.ToString());
            m.WriteMemory("base+373CDF0,3C", "float", state.vSpeed.ToString());

            // Write Lives and rings into memory
            m.WriteMemory("base+0370EF34", "int", state.lives.ToString());
            m.WriteMemory("base+0370F0E4", "int", state.rings.ToString());

            // Write time into memory
            m.WriteMemory("base+0370EF35", "int", state.tFrames.ToString());
            m.WriteMemory("base+0370F128", "int", state.tSeconds.ToString());
            m.WriteMemory("base+0370EF48", "int", state.tMins.ToString());
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
    }


    class SaveState
    {
        public float xPos, yPos, zPos, xRot, yRot, zRot, hSpeed, vSpeed;
        public int lives, rings, tFrames, tSeconds, tMins;
        
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

            this.lives = memwatch.ReadByte("base+0370EF34");
            this.rings = memwatch.Read2Byte("base+0370F0E4");

            this.tFrames = memwatch.ReadByte("base+0370EF35");
            this.tSeconds = memwatch.ReadByte("base+0370F128");
            this.tMins = memwatch.ReadByte("base+0370EF48");

            //Console.WriteLine("Xrot: {0}, Yrot: {1}, Zrot: {2}",this.xRot,this.yRot,this.zRot);

        }
    }

}

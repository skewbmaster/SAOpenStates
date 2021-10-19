using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
using SharpDX.DirectInput;
using SharpDX.XInput;
using DeviceType = SharpDX.DirectInput.DeviceType;
using UserSettings = SADXOpenStates.Properties.Settings;


namespace SADXOpenStates
{
    static partial class Program
    {
        private static Process gameProc = new Process();
        private static XController CONTROLLER; // Initialise controller for inputs
        private static DController DCONTROLLER;
        private static int baseAddress;

        private static bool gameHooked;
        
        private static SaveState[] saveStates = new SaveState[10]; // Allocate 10 save states
        private static int curSaveState;

        private static bool isDInput;

        static void Main(string[] args)
        {
            /*if (File.Exists("states.json")) // Check if states.json exists and if so, read from it and parse JSON data for savestates
            {
                using (StreamReader reader = File.OpenText("states.json"))
                using (JsonTextReader treader = new JsonTextReader(reader))
                {
                    JObject jsonObj = (JObject)JToken.ReadFrom(treader);
                    //jsonObj.Value<string>("Version"); //SaveState file version, useful for later when there's gonna be updates to the savestates themselves
                    
                    saveStates = jsonObj["SaveStates"]?.ToObject<SaveState[]>();
                }
            }*/

            if (File.Exists("save.states"))
            {
                saveStates = SaveStateSerialization.DeserializeStates();
            }

            if (File.Exists("DInput.txt")) // If DInput is configured (DInput file exists) then use the DInput code
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

        private static bool Hook()
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

        private static void ConnectController()
        {
            Console.WriteLine("Searching for XInput controller...");

            while (true)
            {
                CONTROLLER = new XController(UserIndex.One); // Define controller as the first one plugged in

                if (CONTROLLER.GetConnected()) break;

                Thread.Sleep(100);
            }

            Console.WriteLine("Controller found!");
        }

        private static void LoadState(SaveState state)
        {
            // Write Position into memory
            gameProc.Write(gameProc.GetFinalAddress(0x03B42E10, 0x20), state.pos);

            // Write Rotation into memory
            gameProc.Write(gameProc.GetFinalAddress(0x03B42E10, 0x14), state.rot);

            IntPtr speedPointer = gameProc.GetFinalAddress(0x03B3CDF0, 0x0);
            // Write Speed into memory
            gameProc.Write(speedPointer + 0x38, state.speed);
            gameProc.Write(speedPointer + 0x08, state.hover);

            // Write Lives and rings into memory
            gameProc.Write(0x03B0EF34, state.lives);
            gameProc.Write(0x03B0F0E4, state.rings);

            // Write time into memory
            gameProc.Write(0x03B0EF35, state.tFrames);
            gameProc.Write(0x03B0F128, state.tSeconds);
            gameProc.Write(0x03B0EF48, state.tMins);

            // Write more player info
            gameProc.Write(speedPointer + 0x124, state.animData);
            gameProc.Write(gameProc.GetFinalAddress(0x03B42E10, 0x0), state.actionData);
            
            // Write camera info into memory
            //gameProc.Write(gameProc.GetFinalAddress(0x03B2CBB0, 0x20), state.camera1);

            // Working free cam address
            gameProc.Write(0x3B2C968, state.freeCamera);
        }
    }

    internal class XController
    {
        private Controller controller;

        public XController(UserIndex index)
        {
            controller = new Controller(index);
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
        private string Version { get; set; }
        private SaveState[] SaveStates { get; set; }
        [JsonConstructor]
        public SaveStateSerializer(SaveState[] saves)
        {
            Version = "3";
            SaveStates = saves;
        }
    }
}

using System;
using System.Threading;
using Newtonsoft.Json;
using SharpDX.DirectInput;

namespace SADXOpenStates
{
    internal class DController
    {
        public int?[,] Buttons;
        public int?[,] DPad;

        private static DirectInput directInput = new DirectInput();
        private static Joystick joystick;

        public bool controllerIsConnected;

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
                foreach (var deviceInstance in directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices))
                    joystickGuid = deviceInstance.InstanceGuid;

                if (joystickGuid == Guid.Empty)
                    foreach (var deviceInstance in directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AllDevices))
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
                return new[] { false, false, false, false, false, false };
            }


            for (int i = 0; i < 4; i++)
            {
                if (DPad[i, 0] == null)
                {
                    states[i] = state.Buttons[(int)DPad[i, 1]];
                }
                else
                {
                    states[i] = state.PointOfViewControllers[(int)DPad[i, 0]] == DPad[i, 1];
                }
            }
            if (Buttons[0, 0] == null)
            {
                states[4] = state.Buttons[(int)Buttons[0, 1]];
            }
            else
            {
                states[4] = state.PointOfViewControllers[(int)Buttons[0, 0]] == Buttons[0, 1];
            }
            states[5] = true;
            return states;
        }
    }
}
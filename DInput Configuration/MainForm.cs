using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using SharpDX;
using SharpDX.DirectInput;

using Newtonsoft.Json;


namespace DInput_Configuration
{
    public partial class MainForm : Form
    {
        public static bool isActive = false; //, controllerIsConnected = false;
        public static string currentButton = "";
        public static DInput_Backend backEnd;
        public static Thread inputGetter;

        public static DController controls;

        public MainForm()
        {
            InitializeComponent();
            LB.Click += (sender, e) => TextBoxClicked(sender, e, true, 0);
            RB.Click += (sender, e) => TextBoxClicked(sender, e, true, 1);
            Back.Click += (sender, e) => TextBoxClicked(sender, e, true, 2);
            Start.Click += (sender, e) => TextBoxClicked(sender, e, true, 3);
            Y.Click += (sender, e) => TextBoxClicked(sender, e, true, 4);
            X.Click += (sender, e) => TextBoxClicked(sender, e, true, 5);
            B.Click += (sender, e) => TextBoxClicked(sender, e, true, 6);
            A.Click += (sender, e) => TextBoxClicked(sender, e, true, 7);

            DPadUp.Click += (sender, e) => TextBoxClicked(sender, e, false, 0);
            DPadLeft.Click += (sender, e) => TextBoxClicked(sender, e, false, 1);
            DPadRight.Click += (sender, e) => TextBoxClicked(sender, e, false, 2);
            DPadDown.Click += (sender, e) => TextBoxClicked(sender, e, false, 3);

            FormClosing += MainForm_FormClosing;
            FormClosed += MainForm_FormClosed;

            if (File.Exists("DInput.txt"))
            {
                string json = System.IO.File.ReadAllText("DInput.txt");
                controls = JsonConvert.DeserializeObject<DController>(json);
            }
            else
            {
                controls = new DController();
            }
            backEnd = new DInput_Backend();
            new Thread(() => { backEnd.InitializeController(); backEnd.controllerIsConnected = true; }).Start();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if(isActive)
            {
                MessageBox.Show("Cancel the current input assignation before closing");
                e.Cancel = true;
            }
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            Environment.Exit(0);
        }

        private void TextBoxClicked(object sender, EventArgs e, bool isButton, int inputToGet)
        {
            /*if we ever need the event args, here there are
            MouseEventArgs eventArgs = (MouseEventArgs)e;*/
            Button senderButton = (Button)sender;

            if (!backEnd.controllerIsConnected && !backEnd.controllerIsInitializing)
            {
                new Thread(() => { backEnd.InitializeController(); }).Start();
                try { inputGetter.Abort(); } catch { }
                senderButton.Text = senderButton.Name;
                currentButton = "";
                Text = "DInput Configurator";
                isActive = false;
                return;
            }
            else if (!backEnd.controllerIsConnected || (senderButton.Name != currentButton && currentButton != ""))
            {
                return;
            }


            if (isActive)
            {
                try { inputGetter.Abort(); } catch { }
                senderButton.Text = senderButton.Name;
                currentButton = "";
                Text = "DInput Configurator";
                isActive = false;
                return;
            }
            else
            {
                senderButton.Text = "Cancel";
                currentButton = senderButton.Name;
                Text = "Press the button you want to assign to " + senderButton.Name;
                isActive = true;
            }

            inputGetter = new Thread(() =>
            {
                bool isSuccess = backEnd.GetInput(inputToGet, isButton);
                //senderButton.Invoke(new MethodInvoker(delegate { senderButton.Text = senderButton.Name; }));
                senderButton.Text = senderButton.Name;
                //Program.form.Invoke(new MethodInvoker(delegate { Program.form.Text = "DInput Configurator"; }));
                Program.form.Text = "DInput Configurator";

                currentButton = "";
                isActive = false;

                if (!isSuccess)
                {
                    backEnd.InitializeController();
                }
            });
            inputGetter.Start();
        }
    }

    public class DInput_Backend
    {
        public static int?[] ButtonsNumber = new int?[] { null, null, null, null, null, null, null, null };
        public static int?[] ButtonsValues = new int?[] { null, null, null, null, null, null, null, null };

        public static int?[] DPadNumber = new int?[] { null, null, null, null }; // Up, down, left, right
        public static int?[] DPadValues = new int?[] { null, null, null, null };

        private static DirectInput directInput = new DirectInput();
        private static Joystick joystick;

        public bool controllerIsConnected = false;
        public bool controllerIsInitializing = false;

        public DInput_Backend()
        {
            if (MainForm.controls != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    ButtonsNumber[i] = MainForm.controls.Buttons[i, 0];
                    ButtonsValues[i] = MainForm.controls.Buttons[i, 1];
                }
                for (int i = 0; i < 4; i++)
                {
                    DPadNumber[i] = MainForm.controls.DPad[i, 0];
                    DPadValues[i] = MainForm.controls.DPad[i, 1];
                }
            }
        }

        public void InitializeController()
        {
            //Program.form.Invoke(new MethodInvoker(delegate { Program.form.Text = "Searching for controllers..."; }));
            Program.form.Text = "Searching for controllers...";

            controllerIsInitializing = true;
            // Initialize DirectInput
            directInput = new DirectInput();

            // Find a Joystick Guid
            var joystickGuid = Guid.Empty;

            // If Joystick not found, throws an error
            while (joystickGuid == Guid.Empty)
            {
                foreach (var deviceInstance in directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices))
                    joystickGuid = deviceInstance.InstanceGuid;

                Thread.Sleep(1000);
            }

            // Instantiate the joystick
            joystick = new Joystick(directInput, joystickGuid);

            // Found Gamepad

            // Set BufferSize in order to use buffered data.
            //joystick.Properties.BufferSize = 0;

            // Acquire the joystick
            joystick.Acquire();
            //Program.form.Invoke(new MethodInvoker(delegate { Program.form.Text = "DInput Configurator"; }));
            Program.form.Text = "DInput Configurator";

            controllerIsConnected = true;
            controllerIsInitializing = false;
        }

        public bool GetInput(int i, bool isButton)
        {
            JoystickState previousState, state;
            try
            {
                previousState = joystick.GetCurrentState();
                state = previousState;
            }
            catch
            {
                MessageBox.Show("The controller was disconnected, Please plug it back and retry");

                controllerIsConnected = false;
                return false;
            }

            int? inputNumber = null;
            int? inputValue = null;

            while (inputValue == null)
            {
                //to know if someone is dumb enough to disconnect it while pressing the button because it's funny
                if (!directInput.IsDeviceAttached(joystick.Information.InstanceGuid))
                {
                    MessageBox.Show("The controller was disconnected, Please plug it back and retry");

                    controllerIsConnected = false;
                    return false;
                }
                else
                {
                    joystick.Poll();
                    state = joystick.GetCurrentState();
                }
                for (int i2 = 0; i2 < state.Buttons.Length; i2++)
                {
                    if (state.Buttons[i2] == true && state.Buttons[i2] != previousState.Buttons[i2])
                    {
                        inputNumber = null;
                        inputValue = i2;
                        break;
                    }
                }
                for (int i3 = 0; i3 < state.PointOfViewControllers.Length; i3++)
                {
                    if (state.PointOfViewControllers[i3] != previousState.PointOfViewControllers[i3])
                    {
                        inputNumber = i3;
                        inputValue = state.PointOfViewControllers[i3];
                        break;
                    }
                }

                previousState = state;
            }

            if (isButton)
            {
                ButtonsNumber[i] = inputNumber;
                ButtonsValues[i] = inputValue;
            }
            else
            {
                DPadNumber[i] = inputNumber;
                DPadValues[i] = inputValue;
            }
            MessageBox.Show("Saved the input");
            /*
            previousState = joystick.GetCurrentState();
            while (DPadValues[i] == null)
            {
                if (!directInput.IsDeviceAttached(joystick.Information.InstanceGuid))
                {
                    Console.WriteLine("controller disconnected oof");
                    Thread.Sleep(5000);
                }
                joystick.Poll();
                state = joystick.GetCurrentState();

                for (int i2 = 0; i2 < state.PointOfViewControllers.Length; i2++)
                {
                    if (state.PointOfViewControllers[i2] != previousState.PointOfViewControllers[i2])
                    {
                        DPadNumber[i] = i2;
                        DPadValues[i] = state.PointOfViewControllers[i2];
                        break;
                    }
                }
                for (int i4 = 0; i4 < state.Buttons.Length; i4++)
                {
                    if (state.Buttons[i4] == true && state.Buttons[i4] != previousState.Buttons[i4])
                    {
                        DPadNumber[i] = null;
                        DPadValues[i] = i4;
                        break;
                    }
                }
                if (DPadValues[i] != null)
                {
                    Console.WriteLine(XDPad[i] + " = " + "DPad " + DPadNumber[i]);
                    Console.WriteLine(state.PointOfViewControllers[0] + " " + state.PointOfViewControllers[1] + " " + state.PointOfViewControllers[2] + " " + state.PointOfViewControllers[3]);

                    Console.WriteLine(DPadValues[i]);
                }
                previousState = state;
            }
            Thread.Sleep(727);*/

            // Save the fucking object
            string json = JsonConvert.SerializeObject(new DController());
            File.WriteAllText("DInput.txt", json);

            return true;
        }
    }
    public class DController
    {
        public int?[,] DPad = new int?[4, 2];
        public int?[,] Buttons = new int?[8, 2];

        public DController()
        {
            for (int i = 0; i < 4; i++)
            {
                DPad[i, 0] = DInput_Backend.DPadNumber[i];
                DPad[i, 1] = DInput_Backend.DPadValues[i];
            }
            for (int i = 0; i < 8; i++)
            {
                Buttons[i, 0] = DInput_Backend.ButtonsNumber[i];
                Buttons[i, 1] = DInput_Backend.ButtonsValues[i];
            }
        }

        [JsonConstructor]
        public DController(int?[,] dpadArray, int?[,] buttonsArray)
        {
            DPad = dpadArray;
            Buttons = buttonsArray;
        }
    }
}
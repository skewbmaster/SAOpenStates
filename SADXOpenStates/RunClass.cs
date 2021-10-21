using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using UserSettings = SADXOpenStates.Properties.Settings;

namespace SADXOpenStates
{
    static partial class Program
    {
        private static void Run()
        {
            bool hasSaved = false, hasLoaded = false, hasSwitched = false;

            int invertCycle = UserSettings.Default.invertCycle ? -1 : 1;

            while (true)
            {
                while (gameHooked)
                {
                    #region getButtons
                    
                    bool dLeft, dRight, dUp, dDown, lb;
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

                        int buttonsPressed = (int)CONTROLLER.GetState().Gamepad.Buttons;

                        dUp = (buttonsPressed & 1) != 0;
                        dDown = (buttonsPressed & 2) != 0;
                        dLeft = (buttonsPressed & 4) != 0;
                        dRight = (buttonsPressed & 8) != 0;
                        lb = (buttonsPressed & 256) != 0;
                    }
                    else
                    {
                        bool[] dinputs = DCONTROLLER.GetState();
                        if (!dinputs[5])
                        {
                            DCONTROLLER.InitializeController();
                            continue;
                        }

                        dUp = dinputs[0];
                        dLeft = dinputs[1];
                        dRight = dinputs[2];
                        dDown = dinputs[3];
                        lb = dinputs[4];
                    }
                    #endregion

                    byte gameState = gameProc.ReadByte(0x3B22DE4);
                    
                    if (!lb && UserSettings.Default.extraInput) continue;
                    
                    if (dLeft && !hasSaved)
                    {
                        if (gameState != 16 && gameState != 21)
                        {
                            saveStates[curSaveState] = new SaveState(ref gameProc, ref baseAddress);

                            Console.WriteLine("Saved to {0}", curSaveState);
                            
                            SaveStateSerialization.SerialiseStates(saveStates);
                        }
                        hasSaved = true;
                    }
                    else if (!dLeft && hasSaved)
                    {
                        hasSaved = false;
                    }

                    if (dRight && !hasLoaded)
                    {
                        if (saveStates[curSaveState] != null)
                        {
                            if (gameState != 16 && gameState != 21)
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
                    else if (!dRight && hasLoaded)
                    {
                        hasLoaded = false;
                    }
                        

                    if ((dUp || dDown) && !hasSwitched)
                    {
                        if (gameState != 16)
                        {
                            //Console.WriteLine(Convert.ToInt32(UserSettings.Default.invertCycle));

                            curSaveState += Convert.ToInt32(dUp) * invertCycle;
                            curSaveState -= Convert.ToInt32(dDown) * invertCycle;

                            switch (curSaveState)
                            {
                                case -1:
                                    curSaveState = 9;
                                    break;
                                case 10:
                                    curSaveState = 0;
                                    break;
                            }

                            Console.WriteLine("Switched to save slot {0}", curSaveState);

                        }
                        hasSwitched = true;
                    }
                    else if (!(dUp || dDown) && hasSwitched)
                    {
                        hasSwitched = false;
                    }

                    Thread.Sleep(10);
                }

                Hook();
            }
        }
    }
}
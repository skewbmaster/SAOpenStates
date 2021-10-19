﻿using System;
using System.Diagnostics;
using Newtonsoft.Json;

namespace SADXOpenStates
{
    public class SaveState
    {
        public byte[] pos, rot, camera1, speed, animData, actionData, freeCamera;

        public byte tFrames, tSeconds, tMins, lives, curLevel, curAct, curChar;
        public short rings, hover;

        public const SaveState Empty = default(SaveState);
        public SaveState(ref Process gameProc, ref int baseAddress)
        {
            IntPtr speedPointer = gameProc.GetFinalAddress(0x03B3CDF0, 0x0);
            
            pos = gameProc.ReadBytes(gameProc.GetFinalAddress(0x03B42E10, 0x20), 12);
            rot = gameProc.ReadBytes(gameProc.GetFinalAddress(0x03B42E10, 0x14), 12);

            speed = gameProc.ReadBytes(speedPointer + 0x38, 12);
            hover = gameProc.ReadInt16(speedPointer + 0x8);

            lives = gameProc.ReadByte(0x03B0EF34);
            rings = gameProc.ReadInt16(0x03B0F0E4);

            tFrames = gameProc.ReadByte(0x03B0EF35);
            tSeconds = gameProc.ReadByte(0x03B0F128);
            tMins = gameProc.ReadByte(0x03B0EF48);

            //camera1 = gameProc.ReadBytes(gameProc.GetFinalAddress(0x03B2CBB0, 0x0), 0x50);
            freeCamera = gameProc.ReadBytes(0x3B2C968, 6);
            
            animData = gameProc.ReadBytes(speedPointer + 0x124, 16);
            actionData = gameProc.ReadBytes(gameProc.GetFinalAddress(0x03B42E10, 0x0), 5);

            curLevel = gameProc.ReadByte(0x3B22DCC);
            curAct = gameProc.ReadByte(0x3B22DEC);
            curChar = gameProc.ReadByte(0x3B22DC0);
        }
        
        [JsonConstructor]
        public SaveState()
        {
            
        }
    }
}
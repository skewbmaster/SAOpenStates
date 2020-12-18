# SADXOpenStates
A tool to help practice things in SADX

- Save and load your position, rotation, speed, hover frames, time, lives and rings
- Save up to 10 different save slots at once (0-9)
- Save with left D-pad, load with right D-pad, cycle save slots with up and down D-pad
- Native XInput implementation, additional DInput implementation

Persistent saving and loading of savestates is possible, allowing sharing and preserving of savestates when using the tool multiple times
In the .config file you can inverse the cycle directions and force D-pad buttons to only work when LB is held down.

Notices: Only use the DInput tool (DInput configuration) if XInput doesn't work by default; you can stop DInput detection by deleting the 'DInput.txt' file in the program directory. Savestates only load correctly in the same act and level.

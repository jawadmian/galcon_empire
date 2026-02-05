We want to use a data driven design for the game. A focus should be on using Godot's resource system to define game data. 

Systems should be loosely coupled, to accomodate for easy modification and extension. 

We should use signals to communicate between systems. 

The game Tick is the most basic time unit for out simulation, every event takes place on a tick. We do this so we can speed up or slow down the game by changing the number of ticks per second.
Autonomous Maze Solving Robot
==========================

This project was to build a robot which will autonomously navigate, learn, and find the goal point in a line
maze. The robot can potentially search the entire maze in trying to find the exit because it must learn
the structure of the maze as it navigates it. There’s no way for the robot to know the best course
through the maze before it tries everything. This is interesting it explores robot learning, and combines it
with PID control for line following, and path planning for navigating what it already knows. Given the
vast amount of different systems that need to work for the robot to work this robot was a huge
challenge.

The project is split into two components. A command & control interface (the brains) written in C#, and a low level sensory/motor program (the brawn) written in NXC [flavor of C]. The sensory program runs directly on the robot and communicates with the "brains" via high level bluetooth commands. I chose C# because I felt that it was a good opportunity to experiement with multithreading in C#. It was also very easy to make a quick UI for monitoring and control purposes.

I have included a paper with more details as to how the robot was designed and implemented.

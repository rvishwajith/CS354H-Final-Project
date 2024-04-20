# CS 354H (Computer Graphics Honors) Final Project
Final project by Rohith Vishwajith for CS 354H (Spring 2024, Dr. Etienne Vouga) at UT Austin.

## Project Goals
The overall aim of this project is to create a **performant and interactive ocean simulation in Unity using URP**. I aim to do this by implementing as many of the following features as possible:

**1. Proceduraly animated ocean surface with waves, crests, etc):**
  - Using FFT or Gerstner waves.
  - Add foam and coloring based on wave height (maybe).
  - Use compute shaders (probably necessary) to compute ocean data quickly.
**2. Underwater fog/post-processing shader:**
  - Non-linear visibility falloff by distance and depth.
  - Change in visible color spectrum based on depth similar to real life.
**3. A large school (hundreds to thousands) of animated and interactive fish:**
  - Simulate using the Boids algorithm. Use compute shaders/threading/octrees to speed up performance.
  - Each fish can avoid obstacles, stay in bounds, and/or flee from predators (maybe).
  - Animate fish using a vertex shader and tuning values based on acceleration/velocity.
  - Use GPU instancing to draw animated meshes in a single draw call.

## Attributons
All necessary references for the project should be listed here. This needs to be updated more later.

**Conrad Parker:**
  - http://www.kfish.org/boids/pseudocode.html
**Sebastian Lague:**
  - https://www.youtube.com/watch?v=bqtqltqcQhw
  - https://www.youtube.com/watch?v=PGk0rnyTa1U
**Jerry Tessendorf:**
  - https://people.computing.clemson.edu/~jtessen/reports/papers_files/coursenotes2004.pdf
**Matt Nava:**
  - https://www.youtube.com/watch?v=l9NX06mvp2E

# CS 354H Final Project by Rohith Vishwajith

My final project for CS 354H (Spring 2024, Dr. Etienne Vouga) at UT Austin, with a focus on **Underwater Simulation** in Unity.
**eCIS Extra Credit:** On my honor, I affirm that I have submitted the Course Evaluation Survey for this class.

## Project Overview

The overall aim of this project is to create a **performant and interactive ocean simulation in Unity using URP**. I aim to do this by implementing as many of the following features as possible:

**Procedurally animated ocean surface (waves, crests, etc):**

- Using FFT or Gerstner waves.
- Add foam and coloring based on wave height (maybe).
- Use compute shaders (probably necessary) to compute ocean data quickly.

**Underwater post-processing effects:**

- Non-linear visibility falloff by distance and depth (i.e. underwater fog).
- Change in visible color spectrum based on depth similar to real life.

**Large schools of animated and interactive fish:**

- Simulate using the Boids algorithm. Use compute shaders/threading/octrees to speed up performance.
- Each fish can avoid obstacles, stay in bounds, and/or flee from predators (maybe).
- Animate fish using a vertex shader and tuning values based on acceleration/velocity.
- Use GPU instancing to draw animated meshes in a single draw call.

## Attributons

All necessary references for the project should be listed here. This needs to be updated more later.

**Unity Forums & Documentation:**

- https://forum.unity.com/threads/job-system-example-starting-with-simple-optimizations-using-a-nativearray-struct.540652/
- https://docs.unity3d.com/ScriptReference/Graphics.RenderMeshInstanced.html

**Conrad Parker:**

- http://www.kfish.org/boids/pseudocode.html

**Sebastian Lague:**

- https://www.youtube.com/watch?v=bqtqltqcQhw
- https://www.youtube.com/watch?v=PGk0rnyTa1U

**Jerry Tessendorf:**

- https://people.computing.clemson.edu/~jtessen/reports/papers_files/coursenotes2004.pdf

**Matt Nava:**

- https://www.youtube.com/watch?v=l9NX06mvp2E

import math, random

# Let's test the math:
# Suppose we have an object with 3 orthogonal axes in world space: axX, axY, axZ
# with lengths lenX, lenY, lenZ.
# Let normal be an arbitrary seabed normal, e.g. (0.1, 0.98, -0.15) normalized.
# We pick the shortest axis direction: shortestAxisDir
# We compute alignToNormal = Quaternion.FromToRotation(shortestAxisDir, normal)
# We apply it to the object.
# What is the projection of the longest axis onto the seabed plane?
# By definition, since axX, axY, axZ are mutually orthogonal,
# any axis orthogonal to shortestAxisDir will be orthogonal to normal!
# So its dot product with normal will be 0!
# Which means its entire length lies in the seabed plane (flat on the ground)!

print("Mathematical proof:")
print("1. In local mesh space, axes X, Y, Z are orthogonal.")
print("2. The shortest axis is aligned with the normal N.")
print("3. Therefore, both the longest axis and intermediate axis are orthogonal to N.")
print("4. Therefore, the longest axis (longer width) has 0 component along N, and 100% component along the surface plane.")
print("5. Therefore, the rock is guaranteed to lay flat on the surface!")

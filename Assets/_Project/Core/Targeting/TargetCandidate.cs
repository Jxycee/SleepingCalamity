// Core, no engine references. What the targeting maths needs to know about one possible target, and about the one looking for it.
// Gameplay builds these each frame (positions from transforms, HasLineOfSight from a raycast); Core never touches the engine.
namespace SleepingCalamity.Core
{
    public struct TargetCandidate
    {
        public int Id;                  // stable per target (Gameplay uses the instance id)
        public float X, Y, Z;           // world position of the target's feet (Y is used for the height band)
        public bool Alive;
        public bool IsElite;            // elites and bosses never lose a lock to range or line of sight
        public bool HasLineOfSight;     // from the seeker's eye to the target's aim point, computed by Gameplay

        public TargetCandidate(int id, float x, float y, float z, bool alive = true, bool isElite = false, bool hasLineOfSight = true)
        {
            Id = id;
            X = x;
            Y = y;
            Z = z;
            Alive = alive;
            IsElite = isElite;
            HasLineOfSight = hasLineOfSight;
        }
    }

    // The looker: feet position and flat facing (FacingX, FacingZ need not be normalised; zero means "no facing", every angle is then 0).
    public struct TargetSeeker
    {
        public float X, Y, Z;
        public float FacingX, FacingZ;

        public TargetSeeker(float x, float y, float z, float facingX, float facingZ)
        {
            X = x;
            Y = y;
            Z = z;
            FacingX = facingX;
            FacingZ = facingZ;
        }
    }
}

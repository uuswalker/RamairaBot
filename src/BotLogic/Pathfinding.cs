using CounterStrikeSharp.API.Modules.Utils;

namespace RamairaBot
{
    public class Pathfinding
    {
        private readonly Vector bombSiteA;
        private readonly Vector bombSiteB;
        private readonly Vector midWaypoint;
        private Vector? bombPosition;

        public Pathfinding(Vector siteA, Vector siteB, Vector mid)
        {
            bombSiteA = siteA;
            bombSiteB = siteB;
            midWaypoint = mid;
            bombPosition = null;
        }

        public void UpdateBombPosition(Vector bombPos) => bombPosition = bombPos;

        public int GetMoveAction(float x, float y, float z, bool nearBombSite)
        {
            Vector currentPos = new Vector(x, y, z);
            Vector target = bombPosition ?? (VectorDistance(currentPos, bombSiteA) < VectorDistance(currentPos, bombSiteB) ? bombSiteA : bombSiteB);
            if (!nearBombSite && VectorDistance(currentPos, midWaypoint) > 200) target = midWaypoint;

            float dx = target.X - currentPos.X;
            float dy = target.Y - currentPos.Y;
            if (dx > 50) return 0; // Forward
            if (dx < -50) return 1; // Back
            if (dy > 50) return 2; // Right
            if (dy < -50) return 10; // Left
            return 11; // Hold kalo deket target
        }

        public int GetFlankAction(float x, float y, float z)
        {
            Vector currentPos = new Vector(x, y, z);
            Vector target = VectorDistance(currentPos, bombSiteA) < VectorDistance(currentPos, bombSiteB) ? bombSiteB : bombSiteA;
            if (VectorDistance(currentPos, midWaypoint) > 200) target = midWaypoint;
            float dx = target.X - currentPos.X;
            float dy = target.Y - currentPos.Y;
            if (dx > 50) return 0;
            if (dx < -50) return 1;
            if (dy > 50) return 2;
            if (dy < -50) return 10;
            return 3;
        }

        public int GetMoveToCover(float x, float y, float z)
        {
            Vector currentPos = new Vector(x, y, z);
            Vector nearestCover = bombSiteA;
            float minDist = VectorDistance(currentPos, bombSiteA);

            float distToB = VectorDistance(currentPos, bombSiteB);
            if (distToB < minDist)
            {
                minDist = distToB;
                nearestCover = bombSiteB;
            }

            float distToMid = VectorDistance(currentPos, midWaypoint);
            if (distToMid < minDist)
            {
                nearestCover = midWaypoint;
            }

            float dx = nearestCover.X - currentPos.X;
            float dy = nearestCover.Y - currentPos.Y;
            if (dx > 50) return 0; // Forward
            if (dx < -50) return 1; // Back
            if (dy > 50) return 2; // Right
            if (dy < -50) return 10; // Left
            return 11; // Hold kalo udah deket cover
        }

        public int GetMoveToAngle(float x, float y, float z) // Adjust crosshair placement
        {
            Vector currentPos = new Vector(x, y, z);
            Vector target = VectorDistance(currentPos, bombSiteA) < VectorDistance(currentPos, bombSiteB) ? bombSiteA : bombSiteB;
            float dx = target.X - currentPos.X;
            float dy = target.Y - currentPos.Y;

            // Simulasi gerak kecil buat aim ke headshot angle
            if (dx > 20) return 0; // Forward
            if (dx < -20) return 1; // Back
            if (dy > 20) return 2; // Right
            if (dy < -20) return 10; // Left
            return 3; // Fire kalo udah deket
        }

        public float VectorDistance(Vector v1, Vector v2)
        {
            float dx = v1.X - v2.X;
            float dy = v1.Y - v2.Y;
            float dz = v1.Z - v2.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
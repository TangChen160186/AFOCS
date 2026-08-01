using System.Drawing;
using Emgu.CV;

namespace VisionToolkit.TemplateMatcher;

internal class BlockMax
{
    public class Block
    {
        public Rectangle Rect;
        public double DMax;
        public Point PtMaxLoc;
    }

    public List<Block> VecBlock = new List<Block>();
    public Mat MatSrc;

    public BlockMax(Mat matSrc, Size sizeTemplate)
    {
        MatSrc = matSrc;
        int iBlockW = sizeTemplate.Width * 2;
        int iBlockH = sizeTemplate.Height * 2;

        int iCol = matSrc.Width / iBlockW;
        bool bHResidue = matSrc.Width % iBlockW != 0;
        int iRow = matSrc.Height / iBlockH;
        bool bVResidue = matSrc.Height % iBlockH != 0;

        if (iCol == 0 || iRow == 0) { VecBlock.Clear(); return; }

        for (int y = 0; y < iRow; y++)
        for (int x = 0; x < iCol; x++)
            VecBlock.Add(CreateBlock(matSrc,
                new Rectangle(x * iBlockW, y * iBlockH, iBlockW, iBlockH)));

        if (bHResidue && bVResidue)
        {
            VecBlock.Add(CreateBlock(matSrc,
                new Rectangle(iCol * iBlockW, 0, matSrc.Width - iCol * iBlockW, matSrc.Height)));
            VecBlock.Add(CreateBlock(matSrc,
                new Rectangle(0, iRow * iBlockH, iCol * iBlockW, matSrc.Height - iRow * iBlockH)));
        }
        else if (bHResidue)
        {
            VecBlock.Add(CreateBlock(matSrc,
                new Rectangle(iCol * iBlockW, 0, matSrc.Width - iCol * iBlockW, matSrc.Height)));
        }
        else if (bVResidue)
        {
            VecBlock.Add(CreateBlock(matSrc,
                new Rectangle(0, iRow * iBlockH, matSrc.Width, matSrc.Height - iRow * iBlockH)));
        }
    }

    private static Block CreateBlock(Mat matSrc, Rectangle rect)
    {
        var block = new Block { Rect = rect };
        using (Mat roi = new Mat(matSrc, rect))
        {
            double minV = 0, maxV = 0;
            Point minP = new Point(), maxP = new Point();
            CvInvoke.MinMaxLoc(roi, ref minV, ref maxV, ref minP, ref maxP);
            block.DMax = maxV;
            block.PtMaxLoc = new Point(maxP.X + rect.X, maxP.Y + rect.Y);
        }
        return block;
    }

    public void UpdateMax(Rectangle rectIgnore)
    {
        if (VecBlock.Count == 0) return;
        foreach (var block in VecBlock)
        {
            Rectangle inter = Rectangle.Intersect(rectIgnore, block.Rect);
            if (inter.Width == 0 && inter.Height == 0) continue;
            using (Mat roi = new Mat(MatSrc, block.Rect))
            {
                double minV = 0, maxV = 0;
                Point minP = new Point(), maxP = new Point();
                CvInvoke.MinMaxLoc(roi, ref minV, ref maxV, ref minP, ref maxP);
                block.DMax = maxV;
                block.PtMaxLoc = new Point(maxP.X + block.Rect.X, maxP.Y + block.Rect.Y);
            }
        }
    }

    public void GetMaxValueLoc(out double dMax, out Point ptMaxLoc)
    {
        if (VecBlock.Count == 0)
        {
            ptMaxLoc = new Point();
            double dummyMin = 0; Point dummyLoc = new Point();
            dMax = 0;
            CvInvoke.MinMaxLoc(MatSrc, ref dummyMin, ref dMax, ref dummyLoc, ref ptMaxLoc);
            return;
        }
        int iIndex = 0;
        dMax = VecBlock[0].DMax;
        for (int i = 1; i < VecBlock.Count; i++)
        {
            if (VecBlock[i].DMax >= dMax) { iIndex = i; dMax = VecBlock[i].DMax; }
        }
        ptMaxLoc = VecBlock[iIndex].PtMaxLoc;
    }
}
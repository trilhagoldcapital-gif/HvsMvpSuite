using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace HvsMvp.App
{
    public class SampleMaskClass
    {
        public bool IsSample { get; set; }
    }

    public class SampleMaskService
    {
        private const int BorderBand = 8;
        private const double MinSat = 0.10;
        private const double GradThr = 20.0;
        private const int MinComponent = 200;

        public (SampleMaskClass[,] mask, Bitmap preview) BuildMask(Bitmap bmp)
        {
            using (var src = Ensure24bpp(bmp))
            {
                int w = src.Width, h = src.Height;
                var rect = new Rectangle(0,0,w,h);
                var data = src.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                byte[] buf;
                int stride, bytes;
                try
                {
                    stride = data.Stride;
                    bytes = stride * h;
                    buf = new byte[bytes];
                    System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buf, 0, bytes);
                }
                finally { src.UnlockBits(data); }

                double br=0,bg=0,bb=0; long bc=0;
                for(int y=0;y<h;y++)
                {
                    for(int x=0;x<w;x++)
                    {
                        bool border = x<BorderBand || x>=w-BorderBand || y<BorderBand || y>=h-BorderBand;
                        if(!border) continue;
                        int off = y*stride + x*3;
                        bb+=buf[off+0]; bg+=buf[off+1]; br+=buf[off+2]; bc++;
                    }
                }
                if(bc==0) bc=1;
                br/=bc; bg/=bc; bb/=bc;

                bool[,] fg = new bool[w,h];
                double tColor = 30.0;

                for(int y=0;y<h;y++)
                {
                    int row=y*stride;
                    for(int x=0;x<w;x++)
                    {
                        int off=row+x*3;
                        double B=buf[off+0], G=buf[off+1], R=buf[off+2];
                        double dColor=Math.Sqrt((R-br)*(R-br)+(G-bg)*(G-bg)+(B-bb)*(B-bb));
                        double grad=0;
                        if(x>0 && x<w-1 && y>0 && y<h-1)
                        {
                            int offL=row+(x-1)*3;
                            int offR=row+(x+1)*3;
                            int offU=(y-1)*stride+x*3;
                            int offD=(y+1)*stride+x*3;
                            int gL=(int)(0.299*buf[offL+2]+0.587*buf[offL+1]+0.114*buf[offL+0]);
                            int gR=(int)(0.299*buf[offR+2]+0.587*buf[offR+1]+0.114*buf[offR+0]);
                            int gU=(int)(0.299*buf[offU+2]+0.587*buf[offU+1]+0.114*buf[offU+0]);
                            int gD=(int)(0.299*buf[offD+2]+0.587*buf[offD+1]+0.114*buf[offD+0]);
                            int gx=gR-gL;
                            int gy=gD-gU;
                            grad=Math.Sqrt(gx*gx+gy*gy);
                        }
                        RgbToHsvFast((byte)R,(byte)G,(byte)B,out _,out double S,out double V);
                        bool candidate = (dColor>tColor)||(grad>GradThr)||(S>MinSat);
                        if(V>0.99 || V<0.03) candidate=false;
                        fg[x,y]=candidate;
                    }
                }

                RemoveBorderTouchingComponents(fg,w,h);
                KeepLargestComponent(fg,w,h,MinComponent);

                var mask = new SampleMaskClass[w,h];
                var preview = new Bitmap(w,h,PixelFormat.Format24bppRgb);
                var pdata=preview.LockBits(rect,ImageLockMode.WriteOnly,PixelFormat.Format24bppRgb);
                try{
                    int outBytes=pdata.Stride*h;
                    byte[] outBuf=new byte[outBytes];
                    for(int y=0;y<h;y++){
                        int row2=y*pdata.Stride;
                        for(int x=0;x<w;x++){
                            bool s=fg[x,y];
                            mask[x,y]=new SampleMaskClass{ IsSample=s };
                            int off=row2+x*3;
                            if(s){
                                outBuf[off]=0; outBuf[off+1]=255; outBuf[off+2]=0;
                            } else {
                                outBuf[off]=0; outBuf[off+1]=0; outBuf[off+2]=0;
                            }
                        }
                    }
                    System.Runtime.InteropServices.Marshal.Copy(outBuf,0,pdata.Scan0,outBytes);
                } finally { preview.UnlockBits(pdata); }

                return (mask, preview);
            }
        }

        private static Bitmap Ensure24bpp(Bitmap src)
        {
            if(src.PixelFormat==PixelFormat.Format24bppRgb) return (Bitmap)src.Clone();
            var clone=new Bitmap(src.Width,src.Height,PixelFormat.Format24bppRgb);
            using(var g=Graphics.FromImage(clone)){ g.DrawImage(src,new Rectangle(0,0,src.Width,src.Height)); }
            return clone;
        }

        private static void RgbToHsvFast(byte r, byte g, byte b, out double h, out double s, out double v)
        {
            double rd=r/255.0, gd=g/255.0, bd=b/255.0;
            double max=Math.Max(rd,Math.Max(gd,bd));
            double min=Math.Min(rd,Math.Min(gd,bd));
            v=max;
            double delta=max-min;
            s=max==0?0:delta/max;
            if(delta==0){ h=0; return; }
            double hue;
            if(max==rd) hue=60*((gd-bd)/delta%6);
            else if(max==gd) hue=60*(((bd-rd)/delta)+2);
            else hue=60*(((rd-gd)/delta)+4);
            if(hue<0) hue+=360;
            h=hue;
        }

        private static void RemoveBorderTouchingComponents(bool[,] fg,int w,int h)
        {
            var q=new Queue<(int,int)>();
            bool[,] vis=new bool[w,h];

            Action<int,int> Enq = (x,y)=>{
                if(x<0||x>=w||y<0||y>=h) return;
                if(vis[x,y]) return;
                if(!fg[x,y]) return;
                vis[x,y]=true;
                q.Enqueue((x,y));
            };

            for(int x=0;x<w;x++){ Enq(x,0); Enq(x,h-1); }
            for(int y=0;y<h;y++){ Enq(0,y); Enq(w-1,y); }

            int[] dx={-1,0,1,-1,1,-1,0,1};
            int[] dy={-1,-1,-1,0,0,1,1,1};

            while(q.Count>0)
            {
                var it=q.Dequeue();
                int cx=it.Item1, cy=it.Item2;
                fg[cx,cy]=false;
                for(int k=0;k<8;k++) Enq(cx+dx[k], cy+dy[k]);
            }
        }

        private static void KeepLargestComponent(bool[,] fg,int w,int h,int minComp)
        {
            bool[,] vis=new bool[w,h];
            int[] dx={-1,0,1,-1,1,-1,0,1};
            int[] dy={-1,-1,-1,0,0,1,1,1};
            int bestCount=0;
            List<(int,int)> bestPixels=new List<(int,int)>();

            for(int y=0;y<h;y++)
            {
                for(int x=0;x<w;x++)
                {
                    if(!fg[x,y]||vis[x,y]) continue;
                    var q=new Queue<(int,int)>();
                    var comp=new List<(int,int)>();
                    vis[x,y]=true;
                    q.Enqueue((x,y)); comp.Add((x,y));
                    while(q.Count>0){
                        var it=q.Dequeue();
                        int cx=it.Item1, cy=it.Item2;
                        for(int k=0;k<8;k++){
                            int nx=cx+dx[k], ny=cy+dy[k];
                            if(nx<0||nx>=w||ny<0||ny>=h) continue;
                            if(vis[nx,ny]) continue;
                            if(!fg[nx,ny]) continue;
                            vis[nx,ny]=true;
                            q.Enqueue((nx,ny));
                            comp.Add((nx,ny));
                        }
                    }
                    if(comp.Count>bestCount){
                        bestCount=comp.Count;
                        bestPixels=comp;
                    }
                }
            }

            for(int y2=0;y2<h;y2++)
                for(int x2=0;x2<w;x2++)
                    fg[x2,y2]=false;

            if(bestCount>=minComp)
            {
                foreach(var p in bestPixels)
                    fg[p.Item1,p.Item2]=true;
            }
        }
    }
}

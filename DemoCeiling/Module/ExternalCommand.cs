using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Demo_Ceiling
{
    [TransactionAttribute(TransactionMode.Manual)]
    [RegenerationAttribute(RegenerationOption.Manual)]
    public class Ceiling : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document revitDoc = commandData.Application.ActiveUIDocument.Document;
            Selection selection = commandData.Application.ActiveUIDocument.Selection;

            List<Element> ceilingElementList = new List<Element>();
            List<ElementId> ElementList = selection.GetElementIds().ToList();
            ElementList.ForEach(x =>
            {
                if ((revitDoc.GetElement(x)).Category != null &&
                (revitDoc.GetElement(x)).Category.Id.IntegerValue.Equals((int)BuiltInCategory.OST_Ceilings))
                {
                    ceilingElementList.Add(revitDoc.GetElement(x));

                }

            });

            Options options = new Options();
            options.DetailLevel = ViewDetailLevel.Fine;
            options.ComputeReferences = true;
            options.IncludeNonVisibleObjects = true;


            GeometryElement geometryElement = ceilingElementList[0].get_Geometry(options);

            EdgeArrayArray edgeArrayArray = new EdgeArrayArray();
            geometryElement.Cast<GeometryObject>().ToList().ForEach(x =>
            {
                if (x is Solid)
                {
                    Solid solid = (Solid)x;
                    //   Double faceArea = 0;
                    solid.Faces.Cast<Face>().ToList().ForEach(y =>
                    {
                        //   faceArea = y.Area;
                        if (y is PlanarFace)
                        {
                            PlanarFace planarFace = (PlanarFace)y;
                            edgeArrayArray = (planarFace.EdgeLoops);
                        }
                    });
                }
            });

            List<double> appLength = new List<double>();
            Dictionary<double, ElementId> appLengthMap = new Dictionary<double, ElementId>();
            EdgeArray outerEdgeArray = new EdgeArray();
            double appoxLength = 0;
            List<EdgeArray> allEgdeArray = edgeArrayArray.Cast<EdgeArray>().ToList();
            edgeArrayArray.Cast<EdgeArray>().ToList().ForEach(x =>
            {
                double aLength = 0;

                x.Cast<Edge>().ToList().ForEach(y =>
                {
                    aLength = aLength + y.ApproximateLength;
                });
                if (aLength > appoxLength)
                {
                    appoxLength = aLength;
                    outerEdgeArray = (EdgeArray)x;
                }
            });

            allEgdeArray.ForEach(x =>
            {
                if (x.Size == outerEdgeArray.Size)
                {
                    allEgdeArray.Remove(x);
                }
            });


            List<Edge> outerEdge = outerEdgeArray.Cast<Edge>().ToList();

            List<XYZ> midPoints = new List<XYZ>();

            for (int j = 0; j < allEgdeArray.Count; j++)
            {
                List<Edge> interEdgesList = allEgdeArray[j].Cast<Edge>().ToList();
                List<Double> projectlengthList = new List<double>();
                List<Curve> CurveList = new List<Curve>();

                List<Line> lineList = new List<Line>();
                List<Line> lineprojectList = new List<Line>();

                foreach (Edge x in allEgdeArray[j])
                {
                    Line mainDir = null;
                    lineList.Add(x.AsCurve() as Line);
                    CurveList.Add(x.AsCurve());
                    XYZ midPoint = new XYZ();
                    XYZ inter = new XYZ();
                    double pLength = 100000000;
                    for (int i = 0; i < outerEdge.Count; i++)
                    {

                        if (((x.AsCurve() as Line).Direction).IsAlmostEqualTo((outerEdge[i].AsCurve() as Line).Direction) ||
                           ((x.AsCurve() as Line).Direction).IsAlmostEqualTo((outerEdge[i].AsCurve() as Line).Direction.Negate()))
                        {
                            midPoint = ((x.AsCurve() as Line).Tessellate()[0] + (x.AsCurve() as Line).Tessellate()[1]) / 2;
                            midPoints.Add(midPoint);
                            inter = (outerEdge[i].AsCurve() as Line).Project(midPoint).XYZPoint;
                            Line projectLine = Line.CreateBound(midPoint, inter);

                            double angle = projectLine.Direction.AngleTo((outerEdge[i].AsCurve() as Line).Direction);

                            if (angle.Equals(90 * Math.PI / 180))
                            {
                                double projectlength = projectLine.Length;
                                projectlengthList.Add(projectlength);
                                if (projectLine.Length < pLength)
                                {
                                    pLength = projectLine.Length;
                                    mainDir = projectLine;
                                }


                            }
                        }
                    }
                    lineprojectList.Add(mainDir);
                }
                double minDistance = projectlengthList.Min();

                if (minDistance > UnitUtils.ConvertToInternalUnits(2000, UnitTypeId.Millimeters))
                {
                    List<Element> detailLines = new List<Element>();
                    for (int i = 0; i < lineList.Count; i++)
                    {
                        Element detailLine = DrawLineAtoB(lineList[i], lineprojectList[i], revitDoc);
                        CurveLoop curvesLoop = CurveLoop.Create(CurveList);
                        detailLines.Add(detailLine);
                    }
                    List<Line> detailLinesList = new List<Line>();
                    detailLines.ForEach(x =>
                    {

                        x.get_Geometry(options).Cast<GeometryObject>().ToList().ForEach(y =>
                        {
                            detailLinesList.Add(y as Line);

                        });
                    });
                    List<Line> drawLine = new List<Line>();

                    for (int i = 0; i < detailLinesList.Count; i++)
                    {
                        var point1 = 0.00;
                        var point2 = 0.00;
                        for (int p = 0; p < detailLinesList.Count; p++)
                        {
                            try
                            {
                                if (!(detailLinesList[i]).Origin.Equals((detailLinesList[p]).Origin))
                                {
                                    double angle = detailLinesList[i].Direction.AngleTo((detailLinesList[p]).Direction);
                                    if (angle.Equals(90 * Math.PI / 180))
                                    {
                                        Line line = convertLineToExtendedLine(detailLinesList[p], 1000);

                                        Double interpoint = line.Project(detailLinesList[i].GetEndPoint(0)).Distance;
                                        if (point1 == 0.00)
                                        {
                                            point1 = interpoint;
                                        }
                                        else
                                        {
                                            point2 = interpoint;
                                        }
                                    }
                                }
                            }
                            catch (Exception)
                            {

                                throw;
                            }
                        }
                        if (point1 > point2)
                        {
                            Line mainLine = convertLineToExtendedLine(detailLinesList[i], point2);
                            Draw_LineAtoB(mainLine, revitDoc);
                        }
                        else
                        {
                            Line mainLine = convertLineToExtendedLine(detailLinesList[i], point1);
                            Draw_LineAtoB(mainLine, revitDoc);
                        }
                    }
                    List<ElementId> detailLinesId = new List<ElementId>();
                    detailLines.ForEach(x =>
                    {
                        detailLinesId.Add(x.Id);
                    });
                    Transaction transaction1 = new Transaction(revitDoc, "Delete");
                    transaction1.Start();
                    revitDoc.Delete(detailLinesId);
                    transaction1.Commit();
                }
                else
                {
                    List<double> legnth = new List<double>();
                    lineList.ForEach(x =>
                    {
                        legnth.Add(x.Length);

                    });
                    legnth = legnth.Distinct().ToList();
                    double area = legnth[0] * legnth[1];
                    string text = string.Format("Area is {0}", area.ToString());
                    FilteredElementCollector fec = new FilteredElementCollector(revitDoc);
                    ElementId textTypeId = (fec.OfCategory(BuiltInCategory.OST_TextNotes).FirstOrDefault()).GetTypeId();
                    using (Transaction textnoteplacement = new Transaction(revitDoc, "textNote"))
                    {
                        textnoteplacement.Start();
                        TextNote.Create(revitDoc, revitDoc.ActiveView.Id, (lineList[0] as Line).Origin, text, textTypeId);
                        textnoteplacement.Commit();
                    }
                }
            }

            for (int i = 0; i < outerEdge.Count; i++)
            {
                Line firstLine = outerEdge[i].AsCurve() as Line;

                XYZ midFirstpoint = (firstLine.Tessellate()[0] + firstLine.Tessellate()[1]) / 2;
                for (int j = 0; j < outerEdge.Count; j++)
                {
                    Line sLine = outerEdge[j].AsCurve() as Line;

                    XYZ midSpoint = (sLine.Tessellate()[0] + sLine.Tessellate()[1]) / 2;
                    if (!midFirstpoint.IsAlmostEqualTo(midSpoint))
                    {
                        if (((firstLine).Direction).IsAlmostEqualTo((sLine).Direction) ||
                           ((firstLine).Direction).IsAlmostEqualTo((sLine).Direction.Negate()))
                        {
                            XYZ inter = (sLine).Project(midFirstpoint).XYZPoint;
                            Line projectLine = Line.CreateBound(midFirstpoint, inter);
                            double angle = projectLine.Direction.AngleTo((sLine).Direction);

                            if (angle.Equals(90 * Math.PI / 180))
                            {
                                if (projectLine.Length < UnitUtils.ConvertToInternalUnits(8000, UnitTypeId.Millimeters))
                                {

                                    XYZ fpoint = sLine.Project(firstLine.Tessellate()[0]).XYZPoint;
                                    Line fEndLine = Line.CreateBound(firstLine.Tessellate()[0], fpoint);
                                    List<Curve> curves = new List<Curve>();
                                    XYZ spoint = sLine.Project(firstLine.Tessellate()[1]).XYZPoint;
                                    Line sEndLine = Line.CreateBound(spoint, firstLine.Tessellate()[1]);
                                    Line msline = Line.CreateBound(fpoint, spoint);
                                    Line fmline = Line.CreateBound(firstLine.Tessellate()[1], firstLine.Tessellate()[0]);
                                    curves.Add(fEndLine);
                                    curves.Add(msline);
                                    curves.Add(sEndLine);
                                    curves.Add(fmline);
                                    List<CurveLoop> curveLoopList = new List<CurveLoop>();
                                    curveLoopList.Add(CurveLoop.Create(curves));
                                    FilteredElementCollector fec1 = new FilteredElementCollector(revitDoc);
                                    Element fildregid = fec1.OfCategory(BuiltInCategory.OST_DetailComponents).WhereElementIsElementType().FirstOrDefault();
                                    Transaction transaction = new Transaction(revitDoc, "fr");
                                    transaction.Start();

                                    FilledRegion filledRegion = FilledRegion.Create(revitDoc, fildregid.Id, revitDoc.ActiveView.Id, curveLoopList);
                                    transaction.Commit();
                                }
                            }
                        }
                    }
                }
            }
            return Result.Succeeded;
        }
        public static Element DrawLineAtoB(Line Drawline, Line dline, Document document)
        {
            XYZ pointA = Drawline.Tessellate()[0];
            XYZ pointB = Drawline.Tessellate()[1];
            XYZ vectorI = (dline.Direction) * (UnitUtils.ConvertToInternalUnits(500, UnitTypeId.Millimeters));

            Transaction t1 = new Transaction(document);
            try
            {
                t1.Start("AA");
                XYZ c = new XYZ(0, 0, 0);
                XYZ v0 = new XYZ(pointA.X - pointB.X, pointA.Y - pointB.Y, pointA.Z - pointB.Z);
                XYZ v1 = new XYZ(pointA.X - c.X, pointA.Y - c.Y, pointA.Z - c.Z);
                Plane plane = Plane.CreateByNormalAndOrigin(v0.CrossProduct(v1), pointA);
                SketchPlane skPlane = SketchPlane.Create(document, plane);
                Curve mLine = Line.CreateBound(pointA, pointB);


                Element newELementID = document.Create.NewDetailCurve(document.ActiveView, mLine);
                ElementTransformUtils.MoveElement(document, newELementID.Id, vectorI);
                t1.Commit();
                return newELementID;

            }
            catch (Exception ex)
            {
                // ErrorLog.ErrorLogEntry(ex, MethodBase.GetCurrentMethod().Name);
                if (t1.GetStatus() == TransactionStatus.Started) { t1.RollBack(); }
                return null;
            }
        }
        public static void Draw_LineAtoB(Line Drawline, Document document)
        {
            XYZ pointA = Drawline.Tessellate()[0];
            XYZ pointB = Drawline.Tessellate()[1];


            Transaction t1 = new Transaction(document);
            try
            {
                t1.Start("AA");
                XYZ c = new XYZ(0, 0, 0);
                XYZ v0 = new XYZ(pointA.X - pointB.X, pointA.Y - pointB.Y, pointA.Z - pointB.Z);
                XYZ v1 = new XYZ(pointA.X - c.X, pointA.Y - c.Y, pointA.Z - c.Z);
                Plane plane = Plane.CreateByNormalAndOrigin(v0.CrossProduct(v1), pointA);
                SketchPlane skPlane = SketchPlane.Create(document, plane);
                //Line mLine = Line.CreateBound(pointA, pointB);
                Curve mLine = Line.CreateBound(pointA, pointB);


                Element newELementID = document.Create.NewDetailCurve(document.ActiveView, mLine);



                t1.Commit();
            }
            catch (Exception ex)
            {
                // ErrorLog.ErrorLogEntry(ex, MethodBase.GetCurrentMethod().Name);
                if (t1.GetStatus() == TransactionStatus.Started) { t1.RollBack(); }
            }
        }
        public static Line convertLineToExtendedLine(Line line, double extendedLength)
        {
            Line extendedLine = Line.CreateBound(Line.CreateUnbound(line.GetEndPoint(0), line.Direction.Negate()).Evaluate(extendedLength, false),
                Line.CreateUnbound(line.GetEndPoint(1), line.Direction).Evaluate(extendedLength, false));
            return extendedLine;

        }

    }
}


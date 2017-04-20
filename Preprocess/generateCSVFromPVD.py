from paraview import simple
reader = simple.OpenDataFile("C:/Users/madsr/Desktop/FireAtriumPVD.pvd")
writer = simple.CreateWriter("C:/Users/madsr/Desktop/FireAtriumCSV/fireAtrium.csv", reader)
writer.WriteAllTimeSteps = 1
writer.FieldAssociation = "Points"
writer.UpdatePipeline()
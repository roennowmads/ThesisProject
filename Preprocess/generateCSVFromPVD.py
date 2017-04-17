from paraview import simple
reader = simple.OpenDataFile("C:/Users/madsr/Desktop/bla.pvd")
writer = simple.CreateWriter("C:/Users/madsr/Desktop/transient_data/foo.csv", reader)
writer.WriteAllTimeSteps = 1
writer.FieldAssociation = "Points"
writer.UpdatePipeline()



#f = open("foo0.0.csv")

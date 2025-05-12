namespace MyMvcProject.Models
{
    public class Patient
    {
        public int PatientId { get; set; }
        public required string PatientName { get; set; }
        public required string PatientAddress { get; set; }
        public required string PatientEmail { get; set; }
        public required string PatientPhone { get; set; }
        public required string PatientGender { get; set; }
        public required string BirthDate { get; set; }
        public required string PatientAge { get; set; }

        // PatientHealthCard is auto generated 6 digits
        public required string PatientHealthCard { get; set; }




        public string? location_lat { get; set; }
        public string? location_long { get; set; }






    }

}
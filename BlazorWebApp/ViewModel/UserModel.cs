namespace DA_Ecommershop.Models
{
    public class UserModel
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public DateTime JoinedDate { get; set; }
        public bool IsActive { get; set; }
        public string AvatarUrl { get; set; }
        public string Address { get; set; }
        
        public string Initials => string.Join("", FullName?.Split(' ').Select(n => n[0]) ?? Array.Empty<char>());
    }
    
    public class RoleModel
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }
}

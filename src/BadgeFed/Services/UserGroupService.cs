using BadgeFed.Models;

namespace BadgeFed.Services
{
    public class UserGroupService
    {
        private readonly LocalScopedDb _db;

        public UserGroupService(LocalScopedDb db)
        {
            _db = db;
        }

        public List<UserGroup> GetAllUserGroups()
        {
            return _db.GetAllUserGroups();
        }

        public UserGroup? GetUserGroupById(string id)
        {
            return _db.GetUserGroupById(id);
        }

        public void CreateUserGroup(UserGroup userGroup)
        {
            if (string.IsNullOrEmpty(userGroup.Id))
            {
                userGroup.Id = Guid.NewGuid().ToString();
            }
            
            if (userGroup.CreatedAt == default)
            {
                userGroup.CreatedAt = DateTime.UtcNow;
            }

            _db.UpsertUserGroup(userGroup);
        }

        public void UpdateUserGroup(UserGroup userGroup)
        {
            _db.UpsertUserGroup(userGroup);
        }

        public void DeleteUserGroup(string id)
        {
            // Don't allow deletion of the system group
            if (id == "system")
            {
                throw new InvalidOperationException("Cannot delete the system group");
            }

            _db.DeleteUserGroup(id);
        }

        public List<User> GetUsersByGroup(string groupId)
        {
            return _db.GetUsersByGroupId(groupId);
        }

        public int GetUserCountByGroup(string groupId)
        {
            return GetUsersByGroup(groupId).Count;
        }

        public bool UserGroupExists(string id)
        {
            return GetUserGroupById(id) != null;
        }

        public void EnsureDefaultGroupExists()
        {
            var defaultGroup = GetUserGroupById("system");
            if (defaultGroup == null)
            {
                CreateUserGroup(new UserGroup
                {
                    Id = "system",
                    Name = "Default User Group",
                    Description = "Default user group for all users",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
    }
}

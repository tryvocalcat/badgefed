using BadgeFed.Models;

namespace BadgeFed.Services
{
    public class UserService
    {
        private readonly LocalScopedDb _db;

        public UserService(LocalScopedDb db)
        {
            _db = db;
        }

        public List<User> GetAllUsers()
        {
            var users = new List<User>();
            using var connection = _db.GetConnection();
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Users ORDER BY createdAt DESC";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                users.Add(new User
                {
                    Id = reader["id"].ToString()!,
                    Email = reader["email"].ToString()!,
                    GivenName = reader["givenName"].ToString()!,
                    Surname = reader["surname"].ToString()!,
                    CreatedAt = reader["createdAt"] == DBNull.Value ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("createdAt")),
                    Provider = reader["provider"].ToString()!,
                    Role = reader["role"].ToString()!,
                    IsActive = reader["isActive"] == DBNull.Value ? true : reader.GetBoolean(reader.GetOrdinal("isActive")),
                    GroupId = reader["groupId"]?.ToString() ?? "system"
                });
            }

            return users;
        }

        public void UpdateUserRole(string userId, string role)
        {
            using var connection = _db.GetConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Users SET role = @Role WHERE id = @Id";
            command.Parameters.AddWithValue("@Role", role);
            command.Parameters.AddWithValue("@Id", userId);

            command.ExecuteNonQuery();
            transaction.Commit();
        }

        public void UpdateUserStatus(string userId, bool isActive)
        {
            using var connection = _db.GetConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Users SET isActive = @IsActive WHERE id = @Id";
            command.Parameters.AddWithValue("@IsActive", isActive);
            command.Parameters.AddWithValue("@Id", userId);

            command.ExecuteNonQuery();
            transaction.Commit();
        }

        public User? GetUserById(string id)
        {
            return _db.GetUserById(id);
        }

        public List<User> GetUsersByRole(string role)
        {
            var users = new List<User>();
            using var connection = _db.GetConnection();
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Users WHERE role = @Role ORDER BY createdAt DESC";
            command.Parameters.AddWithValue("@Role", role);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                users.Add(new User
                {
                    Id = reader["id"].ToString()!,
                    Email = reader["email"].ToString()!,
                    GivenName = reader["givenName"].ToString()!,
                    Surname = reader["surname"].ToString()!,
                    CreatedAt = reader["createdAt"] == DBNull.Value ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("createdAt")),
                    Provider = reader["provider"].ToString()!,
                    Role = reader["role"].ToString()!,
                    IsActive = reader["isActive"] == DBNull.Value ? true : reader.GetBoolean(reader.GetOrdinal("isActive")),
                    GroupId = reader["groupId"]?.ToString() ?? "system"
                });
            }

            return users;
        }

        public int GetUserCount()
        {
            using var connection = _db.GetConnection();
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Users";

            return Convert.ToInt32(command.ExecuteScalar());
        }

        public int GetActiveUserCount()
        {
            using var connection = _db.GetConnection();
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Users WHERE isActive = 1";

            return Convert.ToInt32(command.ExecuteScalar());
        }

        public void UpdateUserGroup(string userId, string groupId)
        {
            using var connection = _db.GetConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Users SET groupId = @GroupId WHERE id = @Id";
            command.Parameters.AddWithValue("@GroupId", groupId);
            command.Parameters.AddWithValue("@Id", userId);

            command.ExecuteNonQuery();
            transaction.Commit();
        }

        public List<User> GetUsersByGroup(string groupId)
        {
            return _db.GetUsersByGroupId(groupId);
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
            _db.UpsertUserGroup(userGroup);
        }

        public void UpdateUserGroup(UserGroup userGroup)
        {
            _db.UpsertUserGroup(userGroup);
        }

        public void DeleteUserGroup(string id)
        {
            _db.DeleteUserGroup(id);
        }

        public int CountUsersInGroup(string groupId)
        {
            return _db.CountUsersInGroup(groupId);
        }
    }
}

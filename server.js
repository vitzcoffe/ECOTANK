const crypto = require("crypto");
const fs = require("fs");
const http = require("http");
const path = require("path");
const { URL } = require("url");
const mysql = require("mysql2/promise");

const PORT = Number(process.env.PORT || 8080);
const PUBLIC_DIR = __dirname;

const pool = mysql.createPool({
  host: process.env.MYSQL_HOST || "127.0.0.1",
  port: Number(process.env.MYSQL_PORT || 3306),
  user: process.env.MYSQL_USER || "root",
  password: process.env.MYSQL_PASSWORD || "",
  database: process.env.MYSQL_DATABASE || "ecotank",
  waitForConnections: true,
  connectionLimit: 5,
});

const sessions = new Map();

const roleMap = {
  Turista: "Turista",
  "Anfitrión": "Anfitrion",
  Anfitrion: "Anfitrion",
  Administrador: "Administrador",
  "Negocios Locales": "Negocio Local",
  "Negocio Local": "Negocio Local",
};

function parseCookies(req) {
  const header = req.headers.cookie || "";
  return Object.fromEntries(
    header
      .split(";")
      .map((item) => item.trim())
      .filter(Boolean)
      .map((item) => {
        const index = item.indexOf("=");
        return index === -1 ? [item, ""] : [item.slice(0, index), decodeURIComponent(item.slice(index + 1))];
      }),
  );
}

function getSession(req) {
  const cookies = parseCookies(req);
  const sessionId = cookies.ecotank_session;
  return sessionId ? sessions.get(sessionId) : undefined;
}

function json(res, status, data) {
  const body = JSON.stringify(data);
  res.writeHead(status, {
    "Content-Type": "application/json; charset=utf-8",
    "Content-Length": Buffer.byteLength(body),
  });
  res.end(body);
}

function redirect(res, location) {
  res.writeHead(302, { Location: location });
  res.end();
}

function serveFile(req, res, requestPath) {
  const safePath = requestPath === "/" ? "/ecotank-web.html" : requestPath;
  const filePath = path.normalize(path.join(PUBLIC_DIR, safePath));

  if (!filePath.startsWith(PUBLIC_DIR)) {
    res.writeHead(403);
    res.end("Forbidden");
    return;
  }

  if (path.basename(filePath).toLowerCase() === "interfacez.html" && !getSession(req)) {
    redirect(res, "/");
    return;
  }

  fs.readFile(filePath, (err, content) => {
    if (err) {
      res.writeHead(404);
      res.end("Not found");
      return;
    }

    const ext = path.extname(filePath).toLowerCase();
    const contentTypes = {
      ".html": "text/html; charset=utf-8",
      ".js": "text/javascript; charset=utf-8",
      ".css": "text/css; charset=utf-8",
      ".json": "application/json; charset=utf-8",
    };

    res.writeHead(200, {
      "Content-Type": contentTypes[ext] || "application/octet-stream",
    });
    res.end(content);
  });
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    let body = "";
    req.on("data", (chunk) => {
      body += chunk;
      if (body.length > 1_000_000) {
        req.destroy();
        reject(new Error("Request body too large"));
      }
    });
    req.on("end", () => {
      try {
        resolve(body ? JSON.parse(body) : {});
      } catch {
        reject(new Error("JSON invalido"));
      }
    });
    req.on("error", reject);
  });
}

function hashPassword(password) {
  const iterations = 100_000;
  const salt = crypto.randomBytes(16);
  const hash = crypto.pbkdf2Sync(password, salt, iterations, 32, "sha256");
  return `pbkdf2-sha256$${iterations}$${salt.toString("base64")}$${hash.toString("base64")}`;
}

function verifyPassword(password, storedHash) {
  const parts = storedHash.split("$");
  if (parts.length !== 4 || parts[0] !== "pbkdf2-sha256") return false;

  const iterations = Number(parts[1]);
  const salt = Buffer.from(parts[2], "base64");
  const expected = Buffer.from(parts[3], "base64");
  const actual = crypto.pbkdf2Sync(password, salt, iterations, expected.length, "sha256");

  return crypto.timingSafeEqual(actual, expected);
}

async function ensureDefaultRoles() {
  const roles = [
    ["Turista", "Cliente que reserva alojamientos y actividades."],
    ["Anfitrion", "Usuario que publica alojamientos."],
    ["Administrador", "Usuario que gestiona la plataforma."],
    ["Negocio Local", "Comercio asociado a experiencias turisticas."],
  ];

  for (const [nombre, descripcion] of roles) {
    await pool.execute(
      `INSERT INTO roles (nombre, descripcion)
       VALUES (?, ?)
       ON DUPLICATE KEY UPDATE descripcion = VALUES(descripcion)`,
      [nombre, descripcion],
    );
  }
}

async function handleHealth(res) {
  const [versionRows] = await pool.query("SELECT VERSION() AS version");
  const [roleRows] = await pool.query("SELECT COUNT(*) AS total FROM roles");
  const [userRows] = await pool.query("SELECT COUNT(*) AS total FROM usuarios");

  json(res, 200, {
    ok: true,
    version: versionRows[0].version,
    roles: roleRows[0].total,
    usuarios: userRows[0].total,
  });
}

async function handleRegister(req, res) {
  const body = await readBody(req);
  const fullName = String(body.fullName || "").trim();
  const email = String(body.email || "").trim().toLowerCase();
  const phone = String(body.phone || "").trim();
  const password = String(body.password || "");
  const roleName = roleMap[String(body.role || "Turista")] || "Turista";

  if (!fullName) return json(res, 400, { ok: false, message: "Escribe tu nombre completo." });
  if (!email.includes("@")) return json(res, 400, { ok: false, message: "Escribe un correo valido." });
  if (password.length < 6) {
    return json(res, 400, { ok: false, message: "La contrasena debe tener minimo 6 caracteres." });
  }

  await ensureDefaultRoles();

  const [roleRows] = await pool.execute("SELECT id FROM roles WHERE nombre = ? LIMIT 1", [roleName]);
  if (roleRows.length === 0) return json(res, 400, { ok: false, message: "Rol no encontrado." });

  try {
    const [result] = await pool.execute(
      `INSERT INTO usuarios (nombre_completo, email, telefono, password_hash, rol_id)
       VALUES (?, ?, ?, ?, ?)`,
      [fullName, email, phone || null, hashPassword(password), roleRows[0].id],
    );

    json(res, 201, {
      ok: true,
      message: `Usuario creado correctamente. ID ${result.insertId}.`,
      userId: result.insertId,
    });
  } catch (error) {
    if (error && error.code === "ER_DUP_ENTRY") {
      return json(res, 409, { ok: false, message: "Ese correo ya esta registrado." });
    }
    throw error;
  }
}

async function handleLogin(req, res) {
  const body = await readBody(req);
  const email = String(body.email || "").trim().toLowerCase();
  const password = String(body.password || "");

  if (!email || !password) {
    return json(res, 400, { ok: false, message: "Escribe correo y contrasena." });
  }

  const [rows] = await pool.execute(
    `SELECT u.id, u.nombre_completo, u.password_hash, r.nombre AS rol
     FROM usuarios u
     INNER JOIN roles r ON r.id = u.rol_id
     WHERE u.email = ? AND u.activo = 1
     LIMIT 1`,
    [email],
  );

  if (rows.length === 0) {
    return json(res, 401, { ok: false, message: "No encontramos un usuario activo con ese correo." });
  }

  const user = rows[0];
  if (!verifyPassword(password, user.password_hash)) {
    return json(res, 401, { ok: false, message: "La contrasena no coincide." });
  }

  const sessionId = crypto.randomUUID();
  sessions.set(sessionId, {
    id: user.id,
    fullName: user.nombre_completo,
    role: user.rol,
    email,
  });

  const cookie = `ecotank_session=${encodeURIComponent(sessionId)}; HttpOnly; SameSite=Lax; Path=/`;
  const responseBody = JSON.stringify({
    ok: true,
    message: `Bienvenido, ${user.nombre_completo}. Rol: ${user.rol}.`,
    redirectTo: "/interfacez.html",
    user: {
      id: user.id,
      fullName: user.nombre_completo,
      role: user.rol,
    },
  });

  res.writeHead(200, {
    "Content-Type": "application/json; charset=utf-8",
    "Content-Length": Buffer.byteLength(responseBody),
    "Set-Cookie": cookie,
  });
  res.end(responseBody);
}

async function handleMe(req, res) {
  const session = getSession(req);
  if (!session) return json(res, 401, { ok: false, message: "Sesion no iniciada." });

  json(res, 200, {
    ok: true,
    user: session,
  });
}

async function handleLogout(req, res) {
  const cookies = parseCookies(req);
  if (cookies.ecotank_session) sessions.delete(cookies.ecotank_session);

  res.writeHead(200, {
    "Content-Type": "application/json; charset=utf-8",
    "Set-Cookie": "ecotank_session=; HttpOnly; SameSite=Lax; Path=/; Max-Age=0",
  });
  res.end(JSON.stringify({ ok: true, message: "Sesion cerrada." }));
}

const server = http.createServer(async (req, res) => {
  try {
    const url = new URL(req.url, `http://${req.headers.host}`);

    if (req.method === "GET" && url.pathname === "/api/health") return await handleHealth(res);
    if (req.method === "GET" && url.pathname === "/api/me") return await handleMe(req, res);
    if (req.method === "POST" && url.pathname === "/api/register") return await handleRegister(req, res);
    if (req.method === "POST" && url.pathname === "/api/login") return await handleLogin(req, res);
    if (req.method === "POST" && url.pathname === "/api/logout") return await handleLogout(req, res);
    if (req.method === "GET") return serveFile(req, res, url.pathname);

    json(res, 405, { ok: false, message: "Metodo no permitido." });
  } catch (error) {
    console.error(error);
    json(res, 500, { ok: false, message: error.message || "Error interno del servidor." });
  }
});

server.listen(PORT, () => {
  console.log(`ECOTANK web listo en http://localhost:${PORT}`);
});

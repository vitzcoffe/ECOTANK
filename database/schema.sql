-- =====================================================================
-- ECOTANK · Esquema MySQL — Plataforma de Eco Turismo Experiencial
-- ---------------------------------------------------------------------
-- Solo estructura: CREATE DATABASE + CREATE TABLE.
-- NO contiene datos (sin INSERT). Idempotente: usa IF NOT EXISTS.
-- Motor: InnoDB · Charset: utf8mb4 (soporta tildes y emojis).
-- =====================================================================

CREATE DATABASE IF NOT EXISTS `ecotank`
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE `ecotank`;

-- ---------------------------------------------------------------------
-- roles · Turista, Anfitrión, Administrador, Negocios Locales
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `roles` (
    `id`          INT          NOT NULL AUTO_INCREMENT,
    `nombre`      VARCHAR(50)  NOT NULL,
    `descripcion` VARCHAR(255) NULL,
    PRIMARY KEY (`id`),
    UNIQUE KEY `uq_roles_nombre` (`nombre`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- usuarios · cuentas de la plataforma (1 rol por usuario)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `usuarios` (
    `id`              INT          NOT NULL AUTO_INCREMENT,
    `nombre_completo` VARCHAR(150) NOT NULL,
    `email`           VARCHAR(180) NOT NULL,
    `telefono`        VARCHAR(30)  NULL,
    `password_hash`   VARCHAR(255) NOT NULL,
    `rol_id`          INT          NOT NULL,
    `activo`          TINYINT(1)   NOT NULL DEFAULT 1,
    `fecha_registro`  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`),
    UNIQUE KEY `uq_usuarios_email` (`email`),
    KEY `idx_usuarios_rol` (`rol_id`),
    CONSTRAINT `fk_usuarios_rol`
        FOREIGN KEY (`rol_id`) REFERENCES `roles` (`id`)
        ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- ubicaciones · geolocalización (lat/lng) para mapa Leaflet
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `ubicaciones` (
    `id`          INT           NOT NULL AUTO_INCREMENT,
    `nombre`      VARCHAR(150)  NOT NULL,
    `descripcion` TEXT          NULL,
    `latitud`     DECIMAL(10,7) NOT NULL,
    `longitud`    DECIMAL(10,7) NOT NULL,
    `ciudad`      VARCHAR(120)  NULL,
    `pais`        VARCHAR(120)  NULL DEFAULT 'Colombia',
    PRIMARY KEY (`id`),
    KEY `idx_ubicaciones_coords` (`latitud`, `longitud`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- alojamientos · publicados por usuarios con rol Anfitrión
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `alojamientos` (
    `id`             INT           NOT NULL AUTO_INCREMENT,
    `anfitrion_id`   INT           NOT NULL,
    `nombre`         VARCHAR(150)  NOT NULL,
    `descripcion`    TEXT          NULL,
    `tipo`           VARCHAR(80)   NULL,
    `precio_noche`   DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `capacidad`      INT           NOT NULL DEFAULT 1,
    `ubicacion_id`   INT           NULL,
    `imagen_url`     VARCHAR(500)  NULL,
    `disponible`     TINYINT(1)    NOT NULL DEFAULT 1,
    `fecha_creacion` DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`),
    KEY `idx_aloj_anfitrion` (`anfitrion_id`),
    KEY `idx_aloj_ubicacion` (`ubicacion_id`),
    CONSTRAINT `fk_aloj_anfitrion`
        FOREIGN KEY (`anfitrion_id`) REFERENCES `usuarios` (`id`)
        ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT `fk_aloj_ubicacion`
        FOREIGN KEY (`ubicacion_id`) REFERENCES `ubicaciones` (`id`)
        ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- actividades · senderismo, cascadas, camping, etc.
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `actividades` (
    `id`             INT           NOT NULL AUTO_INCREMENT,
    `organizador_id` INT           NOT NULL,
    `nombre`         VARCHAR(150)  NOT NULL,
    `descripcion`    TEXT          NULL,
    `precio`         DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    `duracion_horas` DECIMAL(5,2)  NULL,
    `cupo_maximo`    INT           NOT NULL DEFAULT 1,
    `ubicacion_id`   INT           NULL,
    `imagen_url`     VARCHAR(500)  NULL,
    `disponible`     TINYINT(1)    NOT NULL DEFAULT 1,
    `fecha_creacion` DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`),
    KEY `idx_act_organizador` (`organizador_id`),
    KEY `idx_act_ubicacion` (`ubicacion_id`),
    CONSTRAINT `fk_act_organizador`
        FOREIGN KEY (`organizador_id`) REFERENCES `usuarios` (`id`)
        ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT `fk_act_ubicacion`
        FOREIGN KEY (`ubicacion_id`) REFERENCES `ubicaciones` (`id`)
        ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- negocios_locales · comercios asociados (rol Negocios Locales)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `negocios_locales` (
    `id`             INT          NOT NULL AUTO_INCREMENT,
    `propietario_id` INT          NOT NULL,
    `nombre`         VARCHAR(150) NOT NULL,
    `descripcion`    TEXT         NULL,
    `categoria`      VARCHAR(100) NULL,
    `telefono`       VARCHAR(30)  NULL,
    `ubicacion_id`   INT          NULL,
    `imagen_url`     VARCHAR(500) NULL,
    `fecha_creacion` DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`),
    KEY `idx_neg_propietario` (`propietario_id`),
    KEY `idx_neg_ubicacion` (`ubicacion_id`),
    CONSTRAINT `fk_neg_propietario`
        FOREIGN KEY (`propietario_id`) REFERENCES `usuarios` (`id`)
        ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT `fk_neg_ubicacion`
        FOREIGN KEY (`ubicacion_id`) REFERENCES `ubicaciones` (`id`)
        ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- reservas · de un alojamiento o de una actividad
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `reservas` (
    `id`             INT           NOT NULL AUTO_INCREMENT,
    `turista_id`     INT           NOT NULL,
    `alojamiento_id` INT           NULL,
    `actividad_id`   INT           NULL,
    `fecha_reserva`  DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `fecha_inicio`   DATE          NOT NULL,
    `fecha_fin`      DATE          NULL,
    `num_personas`   INT           NOT NULL DEFAULT 1,
    `estado`         VARCHAR(30)   NOT NULL DEFAULT 'pendiente',
    `total`          DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    PRIMARY KEY (`id`),
    KEY `idx_res_turista` (`turista_id`),
    KEY `idx_res_alojamiento` (`alojamiento_id`),
    KEY `idx_res_actividad` (`actividad_id`),
    CONSTRAINT `fk_res_turista`
        FOREIGN KEY (`turista_id`) REFERENCES `usuarios` (`id`)
        ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT `fk_res_alojamiento`
        FOREIGN KEY (`alojamiento_id`) REFERENCES `alojamientos` (`id`)
        ON DELETE SET NULL ON UPDATE CASCADE,
    CONSTRAINT `fk_res_actividad`
        FOREIGN KEY (`actividad_id`) REFERENCES `actividades` (`id`)
        ON DELETE SET NULL ON UPDATE CASCADE,
    CONSTRAINT `chk_res_objeto`
        CHECK (`alojamiento_id` IS NOT NULL OR `actividad_id` IS NOT NULL)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- pagos · 1..N pagos por reserva (pagos electrónicos)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `pagos` (
    `id`          INT           NOT NULL AUTO_INCREMENT,
    `reserva_id`  INT           NOT NULL,
    `monto`       DECIMAL(12,2) NOT NULL,
    `metodo_pago` VARCHAR(50)   NOT NULL,
    `estado`      VARCHAR(30)   NOT NULL DEFAULT 'pendiente',
    `referencia`  VARCHAR(120)  NULL,
    `fecha_pago`  DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`),
    KEY `idx_pagos_reserva` (`reserva_id`),
    CONSTRAINT `fk_pagos_reserva`
        FOREIGN KEY (`reserva_id`) REFERENCES `reservas` (`id`)
        ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- resenas · calificaciones 1-5 sobre alojamientos o actividades
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `resenas` (
    `id`             INT       NOT NULL AUTO_INCREMENT,
    `usuario_id`     INT       NOT NULL,
    `alojamiento_id` INT       NULL,
    `actividad_id`   INT       NULL,
    `calificacion`   TINYINT   NOT NULL,
    `comentario`     TEXT      NULL,
    `fecha`          DATETIME  NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`),
    KEY `idx_resenas_usuario` (`usuario_id`),
    KEY `idx_resenas_alojamiento` (`alojamiento_id`),
    KEY `idx_resenas_actividad` (`actividad_id`),
    CONSTRAINT `fk_resenas_usuario`
        FOREIGN KEY (`usuario_id`) REFERENCES `usuarios` (`id`)
        ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT `fk_resenas_alojamiento`
        FOREIGN KEY (`alojamiento_id`) REFERENCES `alojamientos` (`id`)
        ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT `fk_resenas_actividad`
        FOREIGN KEY (`actividad_id`) REFERENCES `actividades` (`id`)
        ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT `chk_resenas_calificacion`
        CHECK (`calificacion` BETWEEN 1 AND 5),
    CONSTRAINT `chk_resenas_objeto`
        CHECK (`alojamiento_id` IS NOT NULL OR `actividad_id` IS NOT NULL)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================================
-- Fin del esquema. Sin datos sembrados (los roles base se cargan aparte).
-- =====================================================================

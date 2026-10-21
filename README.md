# PixelHub — R5.A.10 (Persistance Polyglotte)

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![MongoDB](https://img.shields.io/badge/MongoDB-7.0-47A248?logo=mongodb&logoColor=white)](https://www.mongodb.com/)
[![Redis](https://img.shields.io/badge/Redis-7.2-DC382D?logo=redis&logoColor=white)](https://redis.io/)
[![Neo4j](https://img.shields.io/badge/Neo4j-5.18-008CC1?logo=neo4j&logoColor=white)](https://neo4j.com/)

Plateforme de jeu vidéo en ligne démontrant les principes d'une **architecture de persistance polyglotte** (*Polyglot Persistence*). Une seule API ASP.NET Core minimaliste intégrant quatre moteurs de bases de données spécialisés, chacun dédié à un cas d'usage optimal.

---

## 🏗️ Architecture & 4 Moteurs

| Moteur | Rôle Métier | Modèle de Données & Patterns | Port Décalé |
|:---|:---|:---|:---|
| **PostgreSQL** | Données transactionnelles et joueurs | Relationnel (ACID, Entity Framework Core 10) | `15432` |
| **MongoDB** | Catalogue de jeux & métadonnées | Documentaire BSON (`IMongoClient`, agrégations, validation schéma) | `27018` |
| **Redis** | Cache in-memory, classements & matchmaking | Clé-valeur & structures RAM (Sorted Sets, Lists, Décorateur Cache-Aside) | `16379` |
| **Neo4j** | Graphe social & recommandations | Orienté Graphe (Cypher, requêtes de chemins variables `AMI_DE*1..4`) | `17474` (HTTP) / `17687` (Bolt) |

---

## 📡 Endpoints de l'API

### 1. PostgreSQL (Joueurs & Soldes)
- `GET /players` : Récupère la liste des joueurs enregistrés en base relationnelle.

### 2. MongoDB & Cache Redis (Catalogue de Jeux)
- `GET /games` : Liste complète des jeux (avec mise en cache Redis transparente via `CachedGameCatalog`).
- `GET /games/genre/{genre}` : Filtrage documentaire par genre (ex: RPG, Action).
- `GET /games/top/{count}` : Top $N$ des jeux les mieux notés.
- `GET /games/stats` : Pipeline d'agrégation calculant le nombre de jeux et la note moyenne par genre.
- `GET /games/timed` : Mesure de performance comparative (Cache Miss vs Cache Hit).

### 3. Redis (Temps Réel, Leaderboard & Matchmaking)
- `POST /leaderboard/{pseudo}/points/{points}` : Incrémente le score d'un joueur dans le classement global (`ZADD` / `ZINCRBY`).
- `GET /leaderboard/top/{count}` : Extrait les meilleurs scores (`ZREVRANGEBYSCORE`).
- `GET /leaderboard/rank/{pseudo}` : Rang mondial d'un joueur.
- `POST /online/{pseudo}` : Enregistre le battement de cœur (*heartbeat*) du joueur (`ZADD`).
- `GET /online/count` : Purge automatique des inactifs (> 60s) et comptage des joueurs en ligne.
- `POST /matchmaking/{pseudo}` : Ajoute un joueur dans la file d'attente FIFO (`RPUSH`).
- `POST /matchmaking/match` : Tente d'apparier 2 joueurs consécutifs (`LPOP`).

### 4. Neo4j (Réseau Social & Moteur de Recommandation)
- `GET /social/{pseudo}/friends` : Liste des amis directs (relation bidirectionnelle `[:AMI_DE]`).
- `GET /social/{pseudo}/recommendations?depth=2` : Recommande des jeux possédés par le réseau d'amis jusqu'à une profondeur paramétrable (1 à 4 sauts) en excluant les jeux déjà possédés.
- `GET /social/{pseudo}/friend-suggestions` : Suggère de nouveaux amis (amis d'amis non connectés directement).

---

## 🚀 Démarrage Rapide

### 1. Lancer l'infrastructure Docker

```bash
docker compose up -d          # Démarre Postgres, Mongo, Redis et Neo4j
docker compose ps             # Vérifie que les 4 services sont "running"
```

### 2. Lancer l'API .NET

```bash
cd src/PixelHub.Api
dotnet run                    # L'API écoute sur http://localhost:5199
```

### 3. Tester les endpoints

Ouvrez le fichier `src/PixelHub.Api/PixelHub.Api.http` sous Visual Studio, VS Code (extension REST Client) ou Rider, puis cliquez sur **« Send request »**.

---

## 🔐 Identifiants & Configuration

- **Identifiants de développement :** `pixelhub` / `pixelhub_dev` (Neo4j : utilisateur `neo4j`).
- **Configuration :** Variables d'environnement dans `.env` et chaînes de connexion dans `appsettings.json`.

## 🛑 Arrêter les services

```bash
docker compose stop           # Éteint les conteneurs en conservant les volumes de données
```
*Note : `docker compose down -v` supprime l'ensemble des volumes.*

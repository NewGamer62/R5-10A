# Réponses aux questions d'analyse du fichier `docker-compose.yml`

## Q1. Le fichier déclare quatre services alors que le TP d'aujourd'hui n'en utilise qu'un. Pourquoi, à votre avis ?

- **Continuité pédagogique et fil conducteur :** Le projet sert de fil conducteur pour plusieurs séances de TP abordant l'architecture de **persistance polyglotte** (*Polyglot Persistence*). Les commentaires du fichier le confirment :
  - **PostgreSQL** : données transactionnelles (séance 1)
  - **MongoDB** : catalogue de jeux (séance 2)
  - **Redis** : classements, matchmaking, cache (séance 3)
  - **Neo4j** : graphe social et recommandation (séance 4)
- **Simplicité de configuration :** Définir dès le départ l'ensemble de la stack évite d'avoir à modifier la configuration Docker à chaque séance. Les étudiants disposent ainsi immédiatement de l'environnement complet et cohérent pour l'ensemble du module.

---

## Q2. Trois services ont une section volumes, un seul n'en a pas. Lequel, et qu'est-ce que ça implique pour ses données ?

- **Le service concerné :** Il s'agit de **Redis** (`pixelhub-redis`).
- **Implications pour ses données :**
  - Sans volume monté sur l'hôte, les données stockées dans Redis sont **entièrement volatiles**.
  - Si le conteneur est supprimé (par exemple avec `docker compose down`), toutes les données en cache ou clés stockées sont définitivement perdues.
  - Cela reste cohérent avec son rôle principal (cache, classements éphémères, données de session en mémoire), contrairement aux autres bases (Postgres, Mongo, Neo4j) dont les données doivent persister au-delà du cycle de vie des conteneurs grâce à leurs volumes nommés respectifs (`pg_data`, `mongo_data`, `neo4j_data`).

---

## Q3. Que signifie la ligne `"15432:5432"` ? Les deux nombres ne désignent pas la même chose.

La syntaxe Docker pour la redirection de ports est sous la forme `"<port_hôte>:<port_conteneur>"` :
- **`15432` (Port hôte) :** C'est le port ouvert sur votre propre machine (l'hôte). Comme indiqué en tête du fichier compose, ce port est décalé par rapport au port standard (5432) afin d'éviter tout conflit avec un service PostgreSQL qui tournerait déjà localement sur la machine physique de l'étudiant.
- **`5432` (Port conteneur) :** C'est le port d'écoute par défaut du démon PostgreSQL à l'intérieur du conteneur Docker.

**Signification :** Docker redirige les connexions arrivant sur le port `15432` de votre machine hôte vers le port `5432` du conteneur PostgreSQL.

---

## Q4. Les mots de passe sont écrits en clair dans le fichier. Est-ce acceptable ici ? Le serait-ce sur un serveur de production ?

- **Acceptable ici (en TP / développement local) :**
  - **Oui**, car il s'agit d'un environnement d'apprentissage local avec des données de test/factices, sans exposition publique sur Internet.
  - Cela simplifie la prise en main par les étudiants sans nécessiter la mise en place d'une infrastructure complexe de gestion des secrets.
- **En production :**
  - **Absolument non.** Les fichiers de configuration comme `docker-compose.yml` sont généralement archivés sur des dépôts de code (ex. Git). Laisser des identifiants en clair expose le système à des fuites de données et à des compromissions directes.
  - En production, on utilise des outils dédiés à la gestion des secrets (tels que **Docker Secrets**, **Kubernetes Secrets**, **HashiCorp Vault**, ou des variables d'environnement injectées de façon sécurisée via un coffre-fort de secrets ou un pipeline CI/CD).

---

## Q5. Les commandes ci-dessus utilisent `docker compose exec`. Qu'est-ce que ça fait exactement ? Où s'exécute la commande `redis-cli` ?

- **Rôle de `docker compose exec` :**
  - Cette commande permet d'exécuter un processus ou une commande arbitraire **à l'intérieur d'un conteneur déjà en cours d'exécution** (*running*). Docker Compose identifie le conteneur associé au nom du service (ex: `redis`, `postgres`, `mongo`) et transmet la commande au démon Docker pour exécution dans l'environnement isolé de ce conteneur.
- **Où s'exécute `redis-cli` ?**
  - La commande `redis-cli` s'exécute **à l'intérieur du conteneur `pixelhub-redis`** (dans son propre système de fichiers et son espace de nommage réseau/processus), et **non sur la machine hôte**.
  - **Avantage pratique :** Cela évite d'avoir à installer les outils d'administration ou clients en ligne de commande (`redis-cli`, `mongosh`, `pg_isready`, etc.) sur son système d'exploitation hôte : on exploite directement les binaires embarqués dans l'image Docker du service.

---

## Q6. La base de données tourne dans un conteneur Docker, et la chaîne de connexion dit `Host=localhost`. Pourquoi est-ce que ça fonctionne ?

- **Mécanisme de redirection de port (Port Binding) :**
  - Dans le fichier `docker-compose.yml`, la section `ports` de PostgreSQL déclare `"- \"${PG_PORT:-15432}:5432\""`.
  - Docker mappe ainsi le port d'écoute interne `5432` du conteneur sur le port `15432` de l'interface réseau de la **machine hôte** (`localhost`).
  - Lorsque l'API s'exécute sur votre machine et tente de se connecter à `Host=localhost;Port=15432`, elle envoie ses requêtes au port local de l'hôte, que le démon Docker intercepte et achemine fidèlement vers le port `5432` du conteneur PostgreSQL.

---

## Q7. Arrêtez l'application (Ctrl+C), relancez-la. Les trois joueurs sont-ils dupliqués ? Pourquoi ?

- **Non, les trois joueurs ne sont pas dupliqués.**
- **Pourquoi ? (Explication dans le code et la persistance) :**
  1. **Condition d'initialisation dans `Program.cs` :**
     L'insertion des trois joueurs est encapsulée dans une condition :
     ```csharp
     if (!db.Players.Any())
     {
         db.Players.AddRange(...);
         db.SaveChanges();
     }
     ```
     Avant d'insérer, EF Core exécute une requête `SELECT EXISTS(...)` sur la table `Players`. Dès lors que la table contient déjà des enregistrements, `db.Players.Any()` retourne `true` (donc `!db.Players.Any()` est `false`), et l'insertion initiale est ignorée.
  2. **Persistance des données via le volume Docker :**
     Comme PostgreSQL utilise un volume nommé (`pg_data`), les lignes insérées lors du premier démarrage restent écrites et persistées sur le disque même après l'arrêt ou le redémarrage de l'API.



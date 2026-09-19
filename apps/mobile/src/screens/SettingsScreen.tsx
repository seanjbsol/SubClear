import React from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useAuth } from '../AuthContext';
import { API_URL } from '../api';
import { colours } from '../theme';

const roleLabels: Record<string, string> = {
  owner: 'Owner',
  admin: 'Admin',
  contractsManager: 'Contracts manager',
  viewer: 'Viewer'
};

export function SettingsScreen() {
  const { user, logout } = useAuth();

  return (
    <ScrollView style={styles.flex} contentContainerStyle={styles.container}>
      <Text style={styles.title}>Settings</Text>
      <View style={styles.card}>
        <Text style={styles.label}>Organisation</Text>
        <Text style={styles.value}>{user?.organisationName}</Text>
        <Text style={styles.label}>Signed in as</Text>
        <Text style={styles.value}>{user?.fullName}</Text>
        <Text style={styles.meta}>{user?.email}</Text>
        <Text style={styles.label}>Role</Text>
        <Text style={styles.value}>{roleLabels[user?.role ?? ''] ?? user?.role}</Text>
        <Text style={styles.label}>Tenant ID</Text>
        <Text style={styles.mono}>{user?.tenantId}</Text>
        <Text style={styles.label}>API</Text>
        <Text style={styles.mono}>{API_URL}</Text>
      </View>
      <View style={styles.card}>
        <Text style={styles.value}>Tenant isolation</Text>
        <Text style={styles.meta}>
          Every JWT includes tenant_id and role. The API applies EF Core global query filters and explicit TenantId
          checks, so another main contractor cannot see this register.
        </Text>
      </View>
      <Pressable style={styles.button} onPress={() => void logout()}>
        <Text style={styles.buttonText}>Sign out</Text>
      </Pressable>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1, backgroundColor: colours.paper },
  container: { padding: 20 },
  title: { color: colours.navy, fontSize: 28, fontWeight: '800', marginBottom: 16 },
  card: { backgroundColor: colours.white, borderRadius: 12, padding: 16, marginBottom: 16, borderWidth: 1, borderColor: colours.line },
  label: { color: colours.muted, fontWeight: '600', marginTop: 12 },
  value: { color: colours.navy, fontSize: 18, fontWeight: '700', marginTop: 2 },
  meta: { color: colours.muted, marginTop: 6, lineHeight: 20 },
  mono: { color: colours.ink, marginTop: 4, fontSize: 12 },
  button: { backgroundColor: colours.navy, borderRadius: 10, padding: 14, alignItems: 'center' },
  buttonText: { color: colours.white, fontWeight: '700' }
});

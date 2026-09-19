import React, { useCallback, useState } from 'react';
import { FlatList, Pressable, RefreshControl, StyleSheet, Text, TextInput, View } from 'react-native';
import { useFocusEffect } from '@react-navigation/native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useAuth } from '../AuthContext';
import { colours } from '../theme';
import type { DirectoryListing, LinkRequestDto } from '../types';
import type { SubsStackParamList } from '../navigationTypes';

type Props = NativeStackScreenProps<SubsStackParamList, 'Directory'>;

export function DirectoryScreen({ navigation }: Props) {
  const { request, canWrite, entitlements } = useAuth();
  const [rows, setRows] = useState<DirectoryListing[] | null>(null);
  const [email, setEmail] = useState('');
  const [name, setName] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const load = useCallback(async () => {
    setError(null);
    try {
      setRows(await request<DirectoryListing[]>('/api/directory'));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not load the directory.');
    }
  }, [request]);

  useFocusEffect(
    useCallback(() => {
      void load();
    }, [load])
  );

  const linkListing = async (listing: DirectoryListing) => {
    setError(null);
    try {
      const result = await request<LinkRequestDto>('/api/directory/link-requests', {
        method: 'POST',
        body: { networkListingId: listing.id }
      });
      if (result.subcontractorId) {
        navigation.navigate('SubcontractorDetail', { id: result.subcontractorId, name: listing.anonymisedName });
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not link that company.');
    }
  };

  const inviteByEmail = async () => {
    setError(null);
    try {
      const companyName = name.trim() || 'Invited subcontractor';
      const result = await request<LinkRequestDto>('/api/directory/link-requests', {
        method: 'POST',
        body: { email: email.trim(), name: name.trim() || undefined }
      });
      setEmail('');
      setName('');
      if (result.subcontractorId) {
        navigation.navigate('SubcontractorDetail', { id: result.subcontractorId, name: companyName });
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not invite.');
    }
  };

  return (
    <View style={styles.flex}>
      <Text style={styles.lede}>
        Link a verified company from the anonymised SubClear network, or invite a subcontractor by email (Pro portal).
      </Text>
      {canWrite ? (
        <View style={styles.composer}>
          <Text style={styles.label}>Invite by email</Text>
          <TextInput style={styles.input} value={name} onChangeText={setName} placeholder="Company name" />
          <TextInput
            style={styles.input}
            value={email}
            onChangeText={setEmail}
            placeholder="accounts@subcontractor.example"
            autoCapitalize="none"
            keyboardType="email-address"
          />
          <Pressable style={styles.button} onPress={() => void inviteByEmail()}>
            <Text style={styles.buttonText}>{entitlements?.hasPortal ? 'Send portal invite' : 'Invite (Pro)'}</Text>
          </Pressable>
        </View>
      ) : null}
      {error ? <Text style={styles.error}>{error}</Text> : null}
      <FlatList
        data={rows ?? []}
        keyExtractor={(item) => item.id}
        contentContainerStyle={styles.list}
        refreshControl={
          <RefreshControl
            refreshing={refreshing}
            onRefresh={async () => {
              setRefreshing(true);
              await load();
              setRefreshing(false);
            }}
          />
        }
        renderItem={({ item }) => (
          <View style={styles.card}>
            <Text style={styles.name}>{item.anonymisedName}</Text>
            <Text style={styles.meta}>
              {item.trade} · {item.region}
            </Text>
            {canWrite ? (
              <Pressable style={styles.link} onPress={() => void linkListing(item)}>
                <Text style={styles.linkText}>Link into this register</Text>
              </Pressable>
            ) : null}
          </View>
        )}
        ListEmptyComponent={<Text style={styles.empty}>{rows === null ? 'Loading directory…' : 'No verified companies listed yet.'}</Text>}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1, backgroundColor: colours.paper },
  lede: { marginHorizontal: 16, marginTop: 16, color: colours.muted, lineHeight: 20 },
  composer: { margin: 16, backgroundColor: colours.white, borderRadius: 12, padding: 12, borderWidth: 1, borderColor: colours.line },
  label: { color: colours.muted, fontWeight: '600', marginBottom: 6 },
  input: {
    backgroundColor: colours.paper,
    borderColor: colours.line,
    borderWidth: 1,
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 10,
    marginBottom: 10,
    color: colours.ink
  },
  button: { backgroundColor: colours.navy, borderRadius: 10, padding: 12, alignItems: 'center' },
  buttonText: { color: colours.white, fontWeight: '700' },
  list: { padding: 16, paddingBottom: 40 },
  card: { backgroundColor: colours.white, borderRadius: 12, padding: 14, marginBottom: 10, borderWidth: 1, borderColor: colours.line },
  name: { fontWeight: '700', color: colours.navy, fontSize: 16 },
  meta: { color: colours.muted, marginTop: 6 },
  link: { marginTop: 10, alignSelf: 'flex-start', backgroundColor: colours.navy, borderRadius: 8, paddingHorizontal: 10, paddingVertical: 6 },
  linkText: { color: colours.white, fontWeight: '700' },
  empty: { color: colours.muted, textAlign: 'center', marginTop: 40 },
  error: { color: colours.red, marginHorizontal: 16, marginTop: 8 }
});
